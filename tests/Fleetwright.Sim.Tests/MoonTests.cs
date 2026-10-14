using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Tests;

/// <summary>The moon against independent references: tools/moon_ref.py's Meeus ch. 47 series across 1850-2050 (the
/// night actions among them), the USNO's phases of January 2000, and two eclipses, which put it exactly on the line
/// to the sun: the total lunar of 2000-01-21 from the Earth's centre, the total solar of 1999-08-11 from the place of
/// greatest eclipse, which only the parallax gets right.</summary>
public class MoonTests
{
    // year, month, day, hour, minute, sublunar latitude and longitude, distance km, phase angle (tools/moon_ref.py)
    public static TheoryData<int, int, int, int, int, double, double, double, double> References => new()
    {
        { 1850, 1, 1, 0, 0, 15.6517, 36.3141, 361830, 34.052 },
        { 1890, 7, 1, 12, 0, -23.3575, 164.2009, 362675, 15.295 },
        { 1905, 5, 27, 12, 0, -4.9583, -72.3729, 403346, 105.039 },
        { 1916, 5, 31, 23, 0, 25.9846, -164.5228, 402712, 175.858 },
        { 1942, 8, 8, 14, 40, 18.8442, -79.6060, 404800, 140.803 },
        { 1942, 11, 14, 12, 0, -15.7906, 80.3372, 371114, 100.034 },
        { 1944, 10, 24, 19, 0, -20.9300, -16.5062, 377279, 91.850 },
        { 1950, 9, 23, 0, 0, -17.2600, -36.1102, 384239, 37.699 },
        { 1992, 4, 12, 0, 0, 13.7684, -65.7580, 368410, 69.075 },
        { 2000, 1, 21, 4, 44, 19.7596, -68.2791, 360765, 0.298 },
        { 2050, 12, 21, 6, 0, 3.2389, 179.1155, 370353, 88.916 },
    };

    [Theory]
    [MemberData(nameof(References))]
    public void Sublunar_point_and_phase_match_the_reference(int y, int mo, int d, int h, int mi, double lat, double lon,
        double distKm, double phaseDeg)
    {
        var t = WorldTime.FromUtc(y, mo, d, h, mi);
        var (la, lo, dist) = Moon.Sublunar(t);
        // the Almanac's formulas claim 0.3° in ecliptic longitude, 0.2° in latitude and 0.003° in parallax; seen:
        // 0.23° between the directions (1850), 0.23% in distance (2050), 0.19° in the phase angle
        double off = Earth.Angle(Earth.Up(la, lo), Earth.Up(lat, lon)) * 180 / Math.PI;
        Assert.True(off < 0.3, $"sublunar point {la:0.000}, {lo:0.000} is {off:0.000}° from the reference's");
        Assert.True(Math.Abs(dist / 1000 / distKm - 1) < 0.003, $"distance {dist / 1000:0} km, reference {distKm}");
        Assert.True(Math.Abs(Moon.Phase(t).AngleDeg - phaseDeg) < 0.3, $"phase angle {Moon.Phase(t).AngleDeg:0.000}, reference {phaseDeg}");
    }

    // the USNO's phases of January 2000 (UTC, to the minute)
    [Theory]
    [InlineData(6, 18, 14, 0.0, true)]     // new
    [InlineData(14, 13, 34, 0.5, true)]    // first quarter
    [InlineData(21, 4, 40, 1.0, false)]    // full
    [InlineData(28, 7, 57, 0.5, false)]    // last quarter
    public void Phases_of_January_2000(int day, int h, int mi, double lit, bool waxing)
    {
        var p = Moon.Phase(WorldTime.FromUtc(2000, 1, day, h, mi));
        Assert.True(Math.Abs(p.Lit - lit) < 0.01, $"lit {p.Lit:0.000}, want {lit}");
        // the flag turns at new and full: test it a day on
        Assert.Equal(waxing, Moon.Phase(WorldTime.FromUtc(2000, 1, day + 1, h, mi)).Waxing);
    }

    [Fact]
    public void A_total_lunar_eclipse_puts_the_moon_opposite_the_sun()
    {
        var t = WorldTime.FromUtc(2000, 1, 21, 4, 44);   // greatest eclipse
        double off = Earth.Angle(Moon.Position(t), -Sun.Direction(t)) * 180 / Math.PI;
        // the Earth's shadow is ~0.7° in radius there; at greatest eclipse the moon's centre was 0.3° from the axis
        Assert.True(off < 0.5, $"{off:0.000}° from the antisolar point");
    }

    [Fact]
    public void A_total_solar_eclipse_puts_the_moon_on_the_sun_only_with_the_parallax()
    {
        // greatest eclipse of 1999-08-11: 11:03:05 TD (11:02 UT), 45°04'N 24°18'E, the sun 59° up
        var t = WorldTime.FromUtc(1999, 8, 11, 11, 2);
        var at = Earth.Position(45 + 4 / 60.0, 24.3);
        double topo = Earth.Angle(Moon.Direction(at, t), Sun.Direction(t)) * 180 / Math.PI;
        double geo = Earth.Angle(Moon.Position(t), Sun.Direction(t)) * 180 / Math.PI;
        Assert.True(topo < 0.2, $"from the place, the moon is {topo:0.000}° off the sun");
        Assert.True(geo > 0.4, $"from the Earth's centre, {geo:0.000}°: the parallax should have mattered");
    }

    [Fact]
    public void Direction_is_lower_than_the_geocentric_one_by_the_parallax()
    {
        var t = WorldTime.FromUtc(1916, 5, 31, 23, 0);
        var (lat, lon, dist) = Moon.Sublunar(t);
        double hp = Math.Asin(6_378_140 / dist) * 180 / Math.PI;
        Assert.InRange(hp, 0.89, 1.0);
        // overhead: no parallax
        var below = Earth.Position(lat, lon);
        Assert.True(Earth.Angle(below, Moon.Direction(below, t)) < 1e-9);
        // on the geocentric horizon, 90° from the sublunar point: about the parallax below it
        var edge = Earth.Position(lat - 90 < -90 ? lat + 90 : lat - 90, lon);
        double elev = Math.Asin(Vec3d.Dot(edge.Normalized(), Moon.Direction(edge, t))) * 180 / Math.PI;
        Assert.Equal(-hp * Earth.Radius / 6_378_140, elev, 2);
        Assert.Equal(1.0, Moon.Direction(edge, t).Length, 12);
    }

    [Fact]
    public void A_half_moon_gives_about_a_tenth_of_a_full_ones_light()
    {
        Assert.Equal(1.0, Moon.PhaseLaw(0), 12);
        Assert.InRange(Moon.PhaseLaw(90), 0.08, 0.12);
        Assert.InRange(Moon.PhaseLaw(135), 0.008, 0.015);
        Assert.True(Moon.PhaseLaw(180) < 1e-3);
        Assert.Equal(Moon.PhaseLaw(90) * Moon.Light(1, 0), Moon.Light(1, 90), 12);
    }

    [Fact]
    public void No_moonlight_below_the_horizon()
    {
        var t = WorldTime.FromUtc(1916, 6, 15, 23, 30);   // about full
        Assert.True(Moon.Phase(t).AngleDeg < 15);
        var (lat, lon, _) = Moon.Sublunar(t);
        Assert.Equal(0, Moon.Light(-Earth.Position(lat, lon), t));             // the far side of the Earth
        Assert.Equal(0, Moon.Light(Earth.Position(lat - 91 < -90 ? lat + 91 : lat - 91, lon), t));   // just set
        Assert.Equal(0, Moon.Light(-0.02, 0));
        Assert.True(Moon.Light(0.001, 0) > 0);   // the disc's top is up
    }

    [Fact]
    public void Overhead_it_is_the_phase_law_by_the_distance_and_low_down_dimmer()
    {
        var t = WorldTime.FromUtc(1916, 6, 15, 23, 30);
        var (lat, lon, dist) = Moon.Sublunar(t);
        double r = Moon.MeanDistanceM / (dist - Earth.Radius);
        Assert.Equal(Moon.PhaseLaw(Moon.Phase(t).AngleDeg) * r * r, Moon.Light(Earth.Position(lat, lon), t), 6);
        Assert.Equal(1.0, Moon.Light(1, 0), 12);
        double low = Moon.Light(Math.Sin(10 * Math.PI / 180), 0), lower = Moon.Light(Math.Sin(3 * Math.PI / 180), 0);
        Assert.InRange(low, 0.35, 0.5);   // 5.6 air masses
        Assert.True(lower < low / 1.5);
    }
}
