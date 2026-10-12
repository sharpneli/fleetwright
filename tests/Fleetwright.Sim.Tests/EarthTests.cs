using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Tests;

/// <summary>The sphere: lat/lon round trips, distances at combat range and across the globe, headings, a full circle
/// over a pole, and the ship frame's handedness.</summary>
public class EarthTests
{
    [Fact]
    public void LatLon_round_trips()
    {
        var rng = new Random(1);
        for (int k = 0; k < 10000; k++)
        {
            double lat = rng.NextDouble() * 179.998 - 89.999, lon = rng.NextDouble() * 359.998 - 179.999;
            var (la, lo) = Earth.LatLon(Earth.Position(lat, lon));
            Assert.True(Math.Abs(la - lat) < 1e-9 && Math.Abs(lo - lon) < 1e-9, $"{lat}, {lon} -> {la}, {lo}");
        }
        Assert.Equal(180, Earth.LatLon(Earth.Position(0, -180)).Lon, 9);
        Assert.Equal(90, Earth.LatLon(Earth.Position(90, 37)).Lat, 12);
    }

    [Fact]
    public void Axes_are_where_the_convention_says()
    {
        AssertNear(Vec3d.UnitX, Earth.Up(0, 0), 1e-15);
        AssertNear(Vec3d.UnitY, Earth.Up(0, 90), 1e-15);
        AssertNear(Vec3d.UnitZ, Earth.Up(90, 0), 1e-15);
        AssertNear(Vec3d.UnitY, Earth.East(Earth.Position(0, 0)), 1e-15);
        AssertNear(Vec3d.UnitZ, Earth.North(Earth.Position(0, 0)), 1e-15);
    }

    [Fact]
    public void Distance_is_exact_at_short_range_and_across_the_globe()
    {
        // a metre along the equator, and a quarter and a half of the way round
        double m = 1 / Earth.Radius * 180 / Math.PI;
        Assert.Equal(1.0, Earth.Distance(Earth.Position(0, 10), Earth.Position(0, 10 + m)), 9);
        Assert.Equal(Math.PI / 2 * Earth.Radius, Earth.Distance(Earth.Position(0, 0), Earth.Position(0, 90)), 6);
        Assert.Equal(Math.PI * Earth.Radius, Earth.Distance(Earth.Position(10, 20), Earth.Position(-10, -160)), 6);
        // heights don't count: the distance is along the sea under the points
        Assert.Equal(1.0, Earth.Distance(Earth.Position(0, 10, 30), Earth.Position(0, 10 + m)), 9);
    }

    [Fact]
    public void Heading_round_trips_through_forward()
    {
        var p = Earth.Position(56.7, 5.8);
        for (double h = 0; h < 360; h += 7.5)
            Assert.Equal(h, Earth.Heading(p, Earth.Forward(p, h)), 9);
        AssertNear(Earth.East(p), Earth.Forward(p, 90), 1e-15);
    }

    [Fact]
    public void A_full_circle_over_a_pole_comes_back_where_it_started()
    {
        // from the North Sea due north, over the pole, down the far side and back up: 40,030 km in 1 km steps
        var start = Earth.Position(56.7, 5.8);
        var pos = start;
        var fwd = Earth.Forward(start, 0);
        double circle = 2 * Math.PI * Earth.Radius;
        int n = (int)(circle / 1000);
        bool passedPole = false;
        for (int k = 0; k < n; k++)
        {
            Earth.Advance(ref pos, ref fwd, 1000);
            passedPole |= Earth.LatLon(pos).Lat > 89.99;
        }
        Earth.Advance(ref pos, ref fwd, circle - n * 1000.0);
        Assert.True(passedPole);
        Assert.True(Earth.Distance(start, pos) < 1e-3, $"{Earth.Distance(start, pos)} m off");
        // rounding walks the radius by micrometres over 40,000 steps (a random walk, not a drift)
        Assert.True(Math.Abs(pos.Length - Earth.Radius) < 1e-4, $"radius off by {pos.Length - Earth.Radius} m");
        double dh = Earth.Heading(pos, fwd);
        Assert.True(Math.Min(dh, 360 - dh) < 1e-6, $"heading {dh}");
    }

    [Fact]
    public void Advance_keeps_forward_a_unit_tangent()
    {
        var pos = Earth.Position(-33, 151);
        var fwd = Earth.Forward(pos, 63);
        for (int k = 0; k < 100000; k++)
            Earth.Advance(ref pos, ref fwd, 0.5);   // 50 km at a 20 Hz tick of 20 knots-ish
        Assert.Equal(1.0, fwd.Length, 14);
        Assert.Equal(0.0, Vec3d.Dot(fwd, pos.Normalized()), 14);
        Assert.Equal(50000, Earth.Distance(Earth.Position(-33, 151), pos), 4);
    }

    [Fact]
    public void Ship_frame_heading_north_has_starboard_east()
    {
        var p = Earth.Position(56.7, 5.8);
        var f = ShipFrame.At(p, Earth.Forward(p, 0));
        AssertNear(Earth.East(p), f.Starboard, 1e-15);
        AssertNear(Earth.North(p), f.Forward, 1e-15);
        AssertNear(p.Normalized(), f.Up, 1e-15);
        // the frame is left-handed: forward × starboard points down
        Assert.Equal(-1, Vec3d.Dot(Vec3d.Cross(f.Forward, f.Starboard), f.Up), 12);
    }

    [Fact]
    public void Ship_frame_maps_points_both_ways()
    {
        var p = Earth.Position(-12, -170);
        var f = ShipFrame.At(p, Earth.Forward(p, 222));
        var w = f.ToWorld(120, -15, 8);
        var (x, y, h) = f.ToLocal(w);
        Assert.Equal(120, x, 9);
        Assert.Equal(-15, y, 9);
        Assert.Equal(8, h, 9);
        // the bow is ahead along the heading, starboard to its right
        Assert.Equal(222, Earth.Heading(p, f.ToWorld(1, 0, 0) - p), 6);
        Assert.Equal(312, Earth.Heading(p, f.ToWorld(0, 1, 0) - p), 6);
    }

    static void AssertNear(Vec3d want, Vec3d got, double tol) =>
        Assert.True((want - got).Length < tol, $"want {want}, got {got}");
}
