using Fleetwright.Map;
using Fleetwright.Sim.Core;

namespace Fleetwright.Tests;

/// <summary>The map's camera in double: picking and projecting agree to a hair from orbit down to 10 px/m, the scale is
/// what the zoom says, zooming keeps the point under the cursor, a drag keeps the grabbed point under it, motion of a
/// centimetre shows as a tenth of a pixel (no jitter, no steps), and the grid allocates nothing.</summary>
public class MapCameraTests
{
    const double W = 1600, H = 900;

    public static TheoryData<double> Zooms => [12_000, 2000, 200, 20, 1, 0.1];

    [Theory]
    [MemberData(nameof(Zooms))]
    public void Pick_and_project_agree(double mpp)
    {
        var view = new MapView(MapCamera.At(56.7, 5.8, mpp), W, H);
        int hits = 0;
        for (double x = 0; x <= W; x += 37)
            for (double y = 0; y <= H; y += 29)
            {
                if (!view.Pick(x, y, out var p))
                    continue;
                hits++;
                Assert.Equal(Earth.Radius, p.Length, 3);
                Assert.True(view.Project(p, out double px, out double py));
                Assert.True(Math.Abs(px - x) < 1e-5 && Math.Abs(py - y) < 1e-5, $"({x}, {y}) -> ({px}, {py})");
            }
        Assert.True(hits > 100);
    }

    [Theory]
    [InlineData(56.7, 5.8)]
    [InlineData(0, 179.99999)]
    [InlineData(-89.9, -30)]
    public void The_scale_is_the_zoom_at_10_px_per_m(double lat, double lon)
    {
        var cam = MapCamera.At(lat, lon, 0.1);
        var view = new MapView(cam, W, H);
        var at = Earth.Position(lat, lon);
        var east = Earth.East(at);
        var north = Earth.North(at);
        Assert.True(view.Project(at, out double x0, out double y0));
        Assert.Equal(W / 2, x0, 6);
        Assert.Equal(H / 2, y0, 6);
        // a metre east is 10 px right, a metre north 10 px up (the sea curves away by 1e-7 m over a metre)
        Assert.True(view.Project(at + east, out double x1, out double y1));
        Assert.Equal(10, x1 - x0, 4);
        Assert.Equal(0, y1 - y0, 4);
        Assert.True(view.Project(at + north, out double x2, out double y2));
        Assert.Equal(-10, y2 - y0, 4);
        Assert.Equal(0, x2 - x0, 4);
    }

    [Fact]
    public void A_centimetre_of_camera_motion_is_a_tenth_of_a_pixel()
    {
        // what jitter would be: a point fixed on the sea, the camera nudged 1 cm at a time, 10 px/m
        var p = Earth.Position(56.7, 5.8);
        var cam = MapCamera.At(56.7, 5.8, 0.1);
        var pos = cam.Target * Earth.Radius;
        var fwd = Earth.Forward(pos, 90);
        double lastX = double.NaN;
        for (int k = 0; k < 200; k++)
        {
            Assert.True(new MapView(cam, W, H).Project(p, out double x, out _));
            if (k > 0)
                Assert.Equal(-0.1, x - lastX, 6);
            lastX = x;
            Earth.Advance(ref pos, ref fwd, 0.01);
            cam.Target = pos.Normalized();
        }
    }

    [Theory]
    [MemberData(nameof(Zooms))]
    public void Zooming_keeps_the_point_under_the_cursor(double mpp)
    {
        var cam = MapCamera.At(56.7, 5.8, mpp);
        cam.Clamp(H);
        double x = 1100, y = 300;
        if (!new MapView(cam, W, H).Pick(x, y, out var before))
            return;   // the cursor is off the globe at this zoom
        foreach (double f in new[] { 0.8, 1.25, 0.5 })
        {
            cam.ZoomAt(f, x, y, W, H);
            Assert.True(new MapView(cam, W, H).Pick(x, y, out var after));
            Assert.True(Earth.Distance(before, after) < Math.Max(1e-3, cam.MetresPerPixel * 1e-3),
                $"{Earth.Distance(before, after)} m off at {cam.MetresPerPixel} m/px");
        }
    }

    [Fact]
    public void A_drag_brings_the_grabbed_point_under_the_cursor()
    {
        var cam = MapCamera.At(10, -40, 500);
        Assert.True(new MapView(cam, W, H).Pick(500, 400, out var grab));
        cam.Drag(grab, 900, 650, W, H);
        Assert.True(new MapView(cam, W, H).Pick(900, 650, out var now));
        Assert.True(Earth.Distance(grab, now) < 50, $"{Earth.Distance(grab, now)} m off");   // well under a pixel
    }

    [Fact]
    public void A_pan_down_the_screen_goes_north_along_the_meridian_a_pixel_to_a_pixel()
    {
        var cam = MapCamera.At(56.7, 5.8, 20);
        Assert.True(new MapView(cam, W, H).Pick(800, 450, out var held));
        cam.Pan(800, 450, 800, 550, W, H);
        var (lat, lon) = Earth.LatLon(cam.Target);
        Assert.Equal(56.7 + 100 * 20 / Earth.Radius * 180 / Math.PI, lat, 9);
        Assert.Equal(5.8, lon, 9);
        // across too, at a table's zoom the point under the cursor stays under it
        cam.Pan(800, 550, 1000, 550, W, H);
        Assert.True(new MapView(cam, W, H).Pick(1000, 550, out var now));
        Assert.True(Earth.Distance(held, now) < 20, $"{Earth.Distance(held, now)} m off");   // a pixel
    }

    [Theory]
    [InlineData(90)]
    [InlineData(-90)]
    [InlineData(89.99)]
    public void At_a_pole_up_and_down_stays_north_and_south(double lat)
    {
        // the map dragged on toward the pole stops there, without spinning; dragged back it leaves at once
        double s = Math.Sign(lat);
        var cam = MapCamera.At(lat, 20, 500);
        cam.Pan(800, 450, 800, 450 + 300 * s, W, H);
        var (at, lon) = Earth.LatLon(cam.Target);
        Assert.Equal(s * MapCamera.MaxLatitude, at, 9);
        if (Math.Abs(lat) < 90)
            Assert.Equal(20, lon, 6);
        cam.Pan(800, 750, 800, 740 - 300 * s, W, H);   // the same distance back, then 10 px more
        var (back, lon2) = Earth.LatLon(cam.Target);
        Assert.True(Math.Abs(back) < MapCamera.MaxLatitude - 1, $"{back}");
        Assert.Equal(lon, lon2, 6);
    }

    [Fact]
    public void Across_the_screen_by_a_pole_spins_the_globe_gently()
    {
        // the cursor on the pole: 100 px across is the turn of a parallel 50 px out, two radians, no more
        var cam = MapCamera.At(89.999, 20, 1);
        Assert.True(new MapView(cam, W, H).Project(Earth.Position(90, 0), out double px, out double py));
        cam.Pan(px, py, px + 100, py, W, H);
        var (lat, lon) = Earth.LatLon(cam.Target);
        Assert.Equal(89.999, lat, 9);
        Assert.Equal(20 - 2 * 180 / Math.PI, lon, 6);
    }

    [Fact]
    public void The_zoom_stays_in_range()
    {
        var cam = MapCamera.At(0, 0, 1e-6);
        cam.Clamp(H);
        Assert.Equal(MapCamera.MinMetresPerPixel, cam.MetresPerPixel);
        cam.MetresPerPixel = 1e9;
        cam.Clamp(H);
        Assert.Equal(MapCamera.MaxAltitude, cam.Altitude(H), 3);
    }

    [Fact]
    public void Grid_spacing_follows_the_zoom()
    {
        Assert.Equal(10, Graticule.Spacing(12_000));
        Assert.Equal(1 / 3600.0, Graticule.Spacing(0.1));
        Assert.Equal("10°", Graticule.Label(10));
        Assert.Equal("10'", Graticule.Label(1 / 6.0));
        Assert.Equal("1\"", Graticule.Label(1 / 3600.0));
    }

    [Fact]
    public void Building_the_grid_allocates_nothing()
    {
        var lines = new MapLines();
        var ticks = new List<Graticule.BorderTick>();
        var views = new[] { 12_000.0, 2000, 20, 0.1 }.Select(z => new MapView(MapCamera.At(56.7, 5.8, z), W, H)).ToArray();
        foreach (var v in views)
        {
            lines.Begin(W, H);
            Graticule.Build(v, lines);
            Graticule.BorderTicks(v, ticks, lines);
            Assert.True(lines.Vertices.Length > 0);
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int k = 0; k < 20; k++)
            foreach (var v in views)
            {
                lines.Begin(W, H);
                Graticule.Build(v, lines);
                Graticule.BorderTicks(v, ticks, lines);
            }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void In_the_world_view_the_grid_gives_way_to_ticks_at_the_edges()
    {
        var lines = new MapLines();
        var ticks = new List<Graticule.BorderTick>();
        var table = new MapView(MapCamera.At(56.7, 5.8, 20), W, H);
        var world = new MapView(MapCamera.At(56.7, 5.8, 0.5), W, H);
        lines.Begin(W, H);
        Graticule.Build(world, lines);
        Assert.Equal(0, lines.Vertices.Length);
        Graticule.BorderTicks(world, ticks, lines);
        Assert.Contains(ticks, t => t.Parallel && t.Edge is 1 or 3);
        Assert.Contains(ticks, t => !t.Parallel && t.Edge is 0 or 2);
        double step = Graticule.Spacing(0.5);
        Assert.All(ticks, t => Assert.Equal(0, Math.IEEERemainder(t.Value / step, 1), 6));
        Graticule.BorderTicks(table, ticks, lines);
        Assert.Empty(ticks);
        Assert.Equal("56°42'10\"N", Graticule.Format(56.7 + 10 / 3600.0, 1 / 360.0, true));
        Assert.Equal("5°48'W", Graticule.Format(-5.8, 1 / 60.0, false));
    }
}
