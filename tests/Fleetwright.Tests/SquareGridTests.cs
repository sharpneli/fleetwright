using Fleetwright.Map;
using Fleetwright.Sim.Core;

namespace Fleetwright.Tests;

/// <summary>The square grid (docs/visuals/grid.md G1): its anchor follows the view while unseen, holds while seen and
/// fades to a new one past the limits; its rows lie on the 10' parallels near the anchor; it takes over from the
/// graticule smoothly over the blend band; and it allocates nothing.</summary>
public class SquareGridTests
{
    const double W = 1600, H = 900;

    static MapView View(double lat, double lon, double mpp) => new(MapCamera.At(lat, lon, mpp), W, H);

    [Fact]
    public void The_anchor_is_free_unseen_held_seen_and_fades_past_its_limits()
    {
        var grid = new SquareGrid();
        grid.Update(View(56.7, 5.8, 2000), 0.016);   // the graticule's zoom: the squares unseen
        Assert.Equal((57, 6), (grid.Current.Lat, grid.Current.Lon));
        grid.Update(View(55.2, 3.3, 2000), 0.016);
        Assert.Equal((55, 3), (grid.Current.Lat, grid.Current.Lon));   // followed the view at once
        Assert.False(grid.Fading);

        grid.Update(View(55.2, 3.3, 30), 0.016);   // a battle's zoom: seen
        Assert.Equal((55, 3), (grid.Current.Lat, grid.Current.Lon));
        grid.Update(View(55.4, 3.9, 30), 0.016);   // past the next node, within the limits: held
        Assert.Equal((55, 3), (grid.Current.Lat, grid.Current.Lon));
        Assert.False(grid.Fading);

        grid.Update(View(55.4, 4.4, 30), 0.016);   // 1.4° east at 55°: the grid's north 1.15° off the view's
        Assert.Equal((55, 4), (grid.Current.Lat, grid.Current.Lon));
        Assert.True(grid.Fading);
        for (int k = 0; k < 30; k++)
            grid.Update(View(55.4, 4.4, 30), 0.016);
        Assert.False(grid.Fading);

        Assert.True(SquareGrid.Past(new SquareGrid.Anchor(55, 4), 56.9, 4));   // 114 nm north
        Assert.False(SquareGrid.Past(new SquareGrid.Anchor(55, 4), 56.5, 4));
    }

    [Fact]
    public void Rows_lie_on_the_ten_minute_parallels_near_the_anchor()
    {
        var a = new SquareGrid.Anchor(57, 6);
        for (int k = -6; k <= 6; k++)
        {
            var (lat, lon) = Earth.LatLon(SquareGrid.OnSea(a, 0, k * 10 * MapUnits.MetresPerNauticalMile));
            double offM = (lat - (57 + k / 6.0)) * Earth.Radius * Math.PI / 180;
            Assert.True(Math.Abs(offM) < 100, $"row {k}: {offM:0} m off its parallel");
            Assert.Equal(6, lon, 9);
        }
        // and the squares are square: 10 nm east and north of the anchor are 10 nm away on the sea
        double east = Earth.Distance(a.Up * Earth.Radius, SquareGrid.OnSea(a, 18_520, 0));
        double north = Earth.Distance(a.Up * Earth.Radius, SquareGrid.OnSea(a, 0, 18_520));
        Assert.Equal(18_520, east, 0);
        Assert.Equal(east, north, 6);
    }

    [Fact]
    public void The_squares_take_over_from_the_graticule_smoothly()
    {
        var cam = MapCamera.At(56.7, 5.8, 1000);
        for (int k = 0; k < 200; k++, cam.MetresPerPixel /= 1.02)
        {
            double a = GridLook.Squares(new MapView(cam, W, H));
            double b = GridLook.Squares(new MapView(cam with { MetresPerPixel = cam.MetresPerPixel / 1.02 }, W, H));
            Assert.True(b >= a && b - a < 0.03, $"{cam.MetresPerPixel} m/px: the squares from {a} to {b}");
        }
        var lines = new MapLines();
        var grid = new SquareGrid();
        var battle = View(56.7, 5.8, 50);
        grid.Update(battle, 0.016);
        lines.Begin(W, H);
        Graticule.Build(battle, lines);
        Assert.Equal(0, lines.Vertices.Length);   // the graticule only at the margin
        grid.Build(battle, lines);
        Assert.True(lines.Vertices.Length > 0);
        var far = View(56.7, 5.8, 2000);
        grid.Update(far, 0.016);
        lines.Begin(W, H);
        grid.Build(far, lines);
        Assert.Equal(0, lines.Vertices.Length);
    }

    [Fact]
    public void The_square_grid_allocates_nothing()
    {
        var lines = new MapLines();
        var grid = new SquareGrid();
        var views = new[] { 250.0, 150, 50, 10 }.Select(z => View(56.7, 5.8, z)).ToArray();
        foreach (var v in views)
        {
            grid.Update(v, 0.016);
            lines.Begin(W, H);
            grid.Build(v, lines);
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int k = 0; k < 20; k++)
            foreach (var v in views)
            {
                grid.Update(v, 0.016);
                lines.Begin(W, H);
                grid.Build(v, lines);
            }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
