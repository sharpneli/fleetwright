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
        double g = Mod360(357.529 + 0.98560028 * d) * Deg;          // mean anomaly
        double q = Mod360(280.459 + 0.98564736 * d);                // mean longitude (aberration folded in)
        double lam = (q + 1.915 * Math.Sin(g) + 0.020 * Math.Sin(2 * g)) * Deg;   // apparent ecliptic longitude
        double eps = (23.439 - 0.00000036 * d) * Deg;               // obliquity of the ecliptic
        double ra = Math.Atan2(Math.Cos(eps) * Math.Sin(lam), Math.Cos(lam)) / Deg;
        double dec = Math.Asin(Math.Sin(eps) * Math.Sin(lam)) / Deg;
        double gmst = Mod360((18.697374558 + 24.06570982441908 * d) * 15);   // Greenwich mean sidereal time
        double lon = Mod360(ra - gmst);
        return (dec, lon > 180 ? lon - 360 : lon);
    }

    static double Mod360(double a)
    {
        a %= 360;
        return a < 0 ? a + 360 : a;
    }
}
