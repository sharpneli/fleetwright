namespace Fleetwright.Sim.Core;

/// <summary>The planet as a sphere: the one truth for the sim, everywhere (docs/battle/plan.md, "The world").
/// Positions are Earth-centred and Earth-fixed: x through 0°N 0°E, y through 0°N 90°E, z through the north pole.
/// Latitudes from map data are used as spherical ones. Angles in the API are degrees; nothing in the sim's state is
/// a latitude, longitude or heading, those are derived for the UI and orders.</summary>
public static class Earth
{
    /// <summary>The mean radius, metres. Sea level is this sphere.</summary>
    public const double Radius = 6_371_000;

    const double Deg = Math.PI / 180;

    /// <summary>The unit vector up (away from the centre) at a latitude and longitude.</summary>
    public static Vec3d Up(double latDeg, double lonDeg)
    {
        double lat = latDeg * Deg, lon = lonDeg * Deg, c = Math.Cos(lat);
        return new Vec3d(c * Math.Cos(lon), c * Math.Sin(lon), Math.Sin(lat));
    }

    /// <summary>The point at a latitude and longitude, at a height over sea level.</summary>
    public static Vec3d Position(double latDeg, double lonDeg, double height = 0) => Up(latDeg, lonDeg) * (Radius + height);

    /// <summary>Latitude in [-90, 90] and longitude in (-180, 180] of a point (or a direction); 0 longitude at the
    /// poles.</summary>
    public static (double Lat, double Lon) LatLon(Vec3d p)
    {
        double h = Math.Sqrt(p.X * p.X + p.Y * p.Y);
        double lat = Math.Atan2(p.Z, h) / Deg;
        double lon = h > 0 ? Math.Atan2(p.Y, p.X) / Deg : 0;
        return (lat, lon == -180 ? 180 : lon);
    }

    /// <summary>The angle between two directions from the centre, radians. atan2 of the cross and dot products, not
    /// acos of the dot: acos loses precision at small angles, and combat ranges are small angles.</summary>
    public static double Angle(Vec3d a, Vec3d b) => Math.Atan2(Vec3d.Cross(a, b).Length, Vec3d.Dot(a, b));

    /// <summary>The great-circle distance at sea level between the points under a and b, metres.</summary>
    public static double Distance(Vec3d a, Vec3d b) => Radius * Angle(a, b);

    /// <summary>The unit vector east at a point. At the exact poles, where east has no meaning, the east of the 0°
    /// meridian's end (+y), so the frame is still a frame.</summary>
    public static Vec3d East(Vec3d p)
    {
        var e = new Vec3d(-p.Y, p.X, 0);   // z × p
        double l = e.Length;
        return l > 1e-9 * p.Length ? e / l : Vec3d.UnitY;
    }

    /// <summary>The unit vector north at a point (up × east).</summary>
    public static Vec3d North(Vec3d p) => Vec3d.Cross(p.Normalized(), East(p));

    /// <summary>The tangent unit vector at a point toward a heading, degrees clockwise from north.</summary>
    public static Vec3d Forward(Vec3d p, double headingDeg)
    {
        double h = headingDeg * Deg;
        return North(p) * Math.Cos(h) + East(p) * Math.Sin(h);
    }

    /// <summary>The heading of a tangent vector at a point, degrees clockwise from north, in [0, 360).</summary>
    public static double Heading(Vec3d p, Vec3d forward)
    {
        double h = Math.Atan2(Vec3d.Dot(forward, East(p)), Vec3d.Dot(forward, North(p))) / Deg;
        return h < 0 ? h + 360 : h;
    }

    /// <summary>Moves a point a distance along the great circle its forward vector starts, keeping its height over
    /// the centre, and turns forward with it. Both rotate about their common axis (the circle's), so nothing breaks
    /// at the poles or the date line. Forward is kept a unit tangent, so repeated steps don't drift.</summary>
    public static void Advance(ref Vec3d position, ref Vec3d forward, double distance)
    {
        double r = position.Length;
        var up = position / r;
        double t = distance / r, c = Math.Cos(t), s = Math.Sin(t);
        var up2 = (up * c + forward * s).Normalized();
        var f2 = forward * c - up * s;
        position = up2 * r;
        forward = (f2 - up2 * Vec3d.Dot(f2, up2)).Normalized();
    }
}
