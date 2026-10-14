namespace Fleetwright.Sim.Core;

/// <summary>Where the moon is and how much of it is lit: a function of world time, not state. The Astronomical
/// Almanac's low-precision formulas (section D, "Low-precision formulae for the Moon's coordinates"): good to about
/// 0.3° in longitude, 0.2° in latitude and 0.3% in distance, a moonrise to a minute or two, far more than night light
/// and silhouettes need. Checked against Meeus's ch. 47 series (tools/moon_ref.py).
/// <para>Unlike the sun, the moon is near (about 60 Earth radii): seen from different places its direction differs by
/// up to a degree (its parallax), so a direction is asked from a place (<see cref="Direction"/>), and on the horizon
/// it stands about a degree lower than the geocentric one.</para></summary>
public static class Moon
{
    const double Deg = Math.PI / 180;

    /// <summary>The Earth's equatorial radius, m: the horizontal parallax is defined on it.</summary>
    const double EquatorialRadiusM = 6_378_140;

    /// <summary>The astronomical unit, m: the sun's distance for the phase angle (its ±1.7% moves the phase by
    /// thousandths of a degree).</summary>
    const double AuM = 149_597_870_700;

    /// <summary>The moon's centre, Earth-centred and Earth-fixed, metres.</summary>
    public static Vec3d Position(WorldTime t)
    {
        double d = t.DaysSinceJ2000;
        Ecliptic(d, out double lam, out double beta, out double dist);
        double eps = Sun.Obliquity(d);
        double cb = Math.Cos(beta), sb = Math.Sin(beta), cl = Math.Cos(lam), sl = Math.Sin(lam);
        double ce = Math.Cos(eps), se = Math.Sin(eps);
        // ecliptic to equatorial (of date), then turned by the sidereal time to Earth-fixed
        double x = cb * cl, y = ce * cb * sl - se * sb, z = se * cb * sl + ce * sb;
        double g = Sun.SiderealTimeDeg(d) * Deg, cg = Math.Cos(g), sg = Math.Sin(g);
        return new Vec3d(x * cg + y * sg, y * cg - x * sg, z) * dist;
    }

    /// <summary>The point where the moon is overhead as seen from the Earth's centre: latitude (the declination) and
    /// longitude, degrees; and its distance from the centre, metres.</summary>
    public static (double Lat, double Lon, double DistanceM) Sublunar(WorldTime t)
    {
        var p = Position(t);
        var (lat, lon) = Earth.LatLon(p);
        return (lat, lon, p.Length);
    }

    /// <summary>The unit vector from a place (Earth-centred metres, e.g. a ship's position) toward the moon,
    /// Earth-fixed. Up · this is the sine of its elevation there, the parallax included.</summary>
    public static Vec3d Direction(Vec3d from, WorldTime t) => (Position(t) - from).Normalized();

    /// <summary>The moon's phase (<see cref="MoonPhase"/>): geocentric, the same for the whole planet.</summary>
    public static MoonPhase Phase(WorldTime t)
    {
        double d = t.DaysSinceJ2000;
        Ecliptic(d, out double lam, out double beta, out double dist);
        double dl = lam - Sun.EclipticLongitude(d);
        double psi = Math.Acos(Math.Clamp(Math.Cos(beta) * Math.Cos(dl), -1, 1));   // elongation from the sun
        double i = Math.Atan2(AuM * Math.Sin(psi), dist - AuM * Math.Cos(psi));    // the sun-moon-Earth angle
        return new MoonPhase(i / Deg, (1 + Math.Cos(i)) / 2, Math.Sin(dl) > 0);
    }

    /// <summary>Geocentric ecliptic longitude and latitude (of date), radians, and distance, m, d days after J2000.0.</summary>
    static void Ecliptic(double d, out double lam, out double beta, out double dist)
    {
        double T = d / 36525;   // Julian centuries
        lam = (218.32 + 481267.881 * T
               + 6.29 * Sin(135.0 + 477198.87 * T) - 1.27 * Sin(259.3 - 413335.36 * T)
               + 0.66 * Sin(235.7 + 890534.22 * T) + 0.21 * Sin(269.9 + 954397.74 * T)
               - 0.19 * Sin(357.5 + 35999.05 * T) - 0.11 * Sin(186.5 + 966404.03 * T)) * Deg;
        beta = (5.13 * Sin(93.3 + 483202.02 * T) + 0.28 * Sin(228.2 + 960400.89 * T)
                - 0.28 * Sin(318.3 + 6003.15 * T) - 0.17 * Sin(217.6 - 407332.21 * T)) * Deg;
        double hp = (0.9508 + 0.0518 * Cos(135.0 + 477198.87 * T) + 0.0095 * Cos(259.3 - 413335.36 * T)
                     + 0.0078 * Cos(235.7 + 890534.22 * T) + 0.0028 * Cos(269.9 + 954397.74 * T)) * Deg;
        dist = EquatorialRadiusM / Math.Sin(hp);
    }

    static double Sin(double deg) => Math.Sin(Sun.Mod360(deg) * Deg);
    static double Cos(double deg) => Math.Cos(Sun.Mod360(deg) * Deg);
}

/// <summary>The moon's phase. <paramref name="AngleDeg"/>: the phase angle, sun-moon-Earth, degrees: 0 full, 180 new
/// (what the moon's brightness follows, steeply: a half moon gives about a tenth of a full one's light, not half).
/// <paramref name="Lit"/>: the fraction of its disc lit, 0 to 1. <paramref name="Waxing"/>: east of the sun, growing
/// (from new to full).</summary>
public readonly record struct MoonPhase(double AngleDeg, double Lit, bool Waxing);
