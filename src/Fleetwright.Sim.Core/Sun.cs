namespace Fleetwright.Sim.Core;

/// <summary>Where the sun is: a function of world time, not state. The Astronomical Almanac's low-precision formulas
/// (USNO, "Approximate Solar Coordinates"): good to about 1' within two centuries of 2000, far more than day, night and
/// shadows need. Checked against the more precise NOAA/Meeus algorithm (tools/sun_ref.py).</summary>
public static class Sun
{
    const double Deg = Math.PI / 180;

    /// <summary>The unit vector from the Earth's centre toward the sun, Earth-fixed (the sun is far enough that it's
    /// the same direction from everywhere on the planet). Up · this is the sine of the sun's elevation there.</summary>
    public static Vec3d Direction(WorldTime t)
    {
        var (lat, lon) = Subsolar(t);
        return Earth.Up(lat, lon);
    }

    /// <summary>The point where the sun is overhead: latitude (the declination) and longitude, degrees.</summary>
    public static (double Lat, double Lon) Subsolar(WorldTime t)
    {
        double d = t.DaysSinceJ2000;
        double lam = EclipticLongitude(d);
        double eps = Obliquity(d);
        double ra = Math.Atan2(Math.Cos(eps) * Math.Sin(lam), Math.Cos(lam)) / Deg;
        double dec = Math.Asin(Math.Sin(eps) * Math.Sin(lam)) / Deg;
        double lon = Mod360(ra - SiderealTimeDeg(d));
        return (dec, lon > 180 ? lon - 360 : lon);
    }

    /// <summary>The sun's apparent ecliptic longitude, radians, d days after J2000.0.</summary>
    internal static double EclipticLongitude(double d)
    {
        double g = Mod360(357.529 + 0.98560028 * d) * Deg;          // mean anomaly
        double q = Mod360(280.459 + 0.98564736 * d);                // mean longitude (aberration folded in)
        return (q + 1.915 * Math.Sin(g) + 0.020 * Math.Sin(2 * g)) * Deg;
    }

    /// <summary>The obliquity of the ecliptic, radians, d days after J2000.0.</summary>
    internal static double Obliquity(double d) => (23.439 - 0.00000036 * d) * Deg;

    /// <summary>Greenwich mean sidereal time, degrees in [0, 360), d days after J2000.0: the turn from the equinox's
    /// frame to the Earth-fixed one.</summary>
    internal static double SiderealTimeDeg(double d) => Mod360((18.697374558 + 24.06570982441908 * d) * 15);

    internal static double Mod360(double a)
    {
        a %= 360;
        return a < 0 ? a + 360 : a;
    }
}
