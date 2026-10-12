using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Tests;

/// <summary>World time against the calendar, and the sun against independent references: tools/sun_ref.py's more
/// precise algorithm across 1850-2050, and the USNO's equinox and solstice instants of 2000.</summary>
public class TimeTests
{
    [Fact]
    public void World_time_counts_from_1850()
    {
        Assert.Equal(0, WorldTime.FromUtc(1850, 1, 1).Ms);
        Assert.Equal(WorldTime.J2000Ms, WorldTime.FromUtc(2000, 1, 1, 12).Ms);
        Assert.Equal(2451545.0, WorldTime.FromUtc(2000, 1, 1, 12).JulianDate);
        Assert.Equal(2396758.5, new WorldTime(0).JulianDate);
        var t = WorldTime.FromUtc(1916, 5, 31, 15, 48, 7) + 250;
        Assert.Equal(new DateTime(1916, 5, 31, 15, 48, 7, 250, DateTimeKind.Utc), t.ToUtc());
        Assert.Equal("1916-05-31 15:48:07.250", t.ToString());
        // before the epoch counts down, rounding toward the past
        Assert.Equal(-1, WorldTime.FromUtc(new DateTime(1849, 12, 31, 23, 59, 59, 999, DateTimeKind.Utc).AddTicks(5)).Ms);
    }

    // year, month, day, hour, minute, subsolar latitude and longitude (tools/sun_ref.py)
    public static TheoryData<int, int, int, int, int, double, double> References => new()
    {
        { 1850, 1, 1, 0, 0, -23.0576, -179.0977 },
        { 1890, 7, 1, 12, 0, 23.1104, 0.8923 },
        { 1905, 5, 27, 5, 0, 21.1809, 104.2107 },
        { 1916, 5, 31, 15, 48, 21.9351, -57.6330 },
        { 1916, 5, 31, 18, 30, 21.9510, -98.1289 },
        { 1950, 9, 23, 0, 0, 0.2398, 178.1747 },
        { 2000, 6, 21, 12, 0, 23.4375, 0.4569 },
        { 2050, 12, 21, 6, 0, -23.4299, 89.4943 },
    };

    [Theory]
    [MemberData(nameof(References))]
    public void Subsolar_point_matches_the_reference(int y, int mo, int d, int h, int mi, double lat, double lon)
    {
        var (la, lo) = Sun.Subsolar(WorldTime.FromUtc(y, mo, d, h, mi));
        double dlon = Math.Abs(lo - lon);
        dlon = Math.Min(dlon, 360 - dlon);
        // the Almanac's formulas claim 1' (0.017°) within two centuries of 2000
        Assert.True(Math.Abs(la - lat) < 0.02, $"latitude {la}, reference {lat}");
        Assert.True(dlon < 0.02, $"longitude {lo}, reference {lon}");
    }

    // the USNO's instants of 2000 (UTC, to the minute): the declination is 0 at an equinox and ±ε at a solstice
    [Theory]
    [InlineData(3, 20, 7, 35, 0.0)]
    [InlineData(6, 21, 1, 48, 23.4393)]
    [InlineData(9, 22, 17, 28, 0.0)]
    [InlineData(12, 21, 13, 37, -23.4393)]
    public void Equinoxes_and_solstices_of_2000(int mo, int d, int h, int mi, double dec)
    {
        var (la, _) = Sun.Subsolar(WorldTime.FromUtc(2000, mo, d, h, mi));
        Assert.True(Math.Abs(la - dec) < 0.02, $"declination {la}, want {dec}");
    }

    [Fact]
    public void Direction_gives_the_elevation_on_the_subsolar_meridian()
    {
        var t = WorldTime.FromUtc(1916, 5, 31, 15, 48);
        var (dec, lon) = Sun.Subsolar(t);
        var sun = Sun.Direction(t);
        Assert.Equal(1.0, sun.Length, 14);
        foreach (double lat in new[] { -60.0, 0, 21.9, 56.7, 80 })
        {
            double elev = Math.Asin(Vec3d.Dot(Earth.Up(lat, lon), sun)) * 180 / Math.PI;
            Assert.Equal(90 - Math.Abs(lat - dec), elev, 9);
        }
        // the opposite meridian at the equator is in the night
        Assert.True(Vec3d.Dot(Earth.Up(0, lon + 180), sun) < 0);
    }
}
