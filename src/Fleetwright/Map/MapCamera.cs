using Fleetwright.Sim.Core;

namespace Fleetwright.Map;

/// <summary>Where the map looks: straight down at a point on the globe, north up, from a height set by the zoom. A
/// plain struct the scene owns; <see cref="MapView"/> resolves it for a target's size. The zoom is metres per pixel
/// at the point looked at, the unit the map's bands are defined in (docs/map-ui/README.md), from the whole globe down
/// to <see cref="MinMetresPerPixel"/> (the sprites' finest level, 10 px/m).</summary>
public struct MapCamera
{
    public const double FovYDeg = 40;
    public const double MinMetresPerPixel = 0.1;

    /// <summary>The highest the camera goes, metres over the sea: the whole globe with room around it.</summary>
    public const double MaxAltitude = 4 * Earth.Radius;

    /// <summary>The unit vector to the point under the screen's centre.</summary>
    public Vec3d Target;

    /// <summary>Metres per pixel at <see cref="Target"/>.</summary>
    public double MetresPerPixel;

    public static readonly double TanHalfY = Math.Tan(FovYDeg / 2 * Math.PI / 180);

    public static MapCamera At(double latDeg, double lonDeg, double metresPerPixel) =>
        new() { Target = Earth.Up(latDeg, lonDeg), MetresPerPixel = metresPerPixel };

    /// <summary>The camera's height over the sea for a target <paramref name="heightPx"/> tall.</summary>
    public readonly double Altitude(double heightPx) => MetresPerPixel * heightPx / (2 * TanHalfY);

    /// <summary>The zoom kept within its range for a target <paramref name="heightPx"/> tall.</summary>
    public void Clamp(double heightPx)
    {
        double max = MaxAltitude * 2 * TanHalfY / heightPx;
        MetresPerPixel = Math.Clamp(MetresPerPixel, MinMetresPerPixel, max);
        Target = Target.Normalized();
    }

    /// <summary>Zooms by <paramref name="factor"/> (above 1 out), keeping the globe's point under (x, y) where it is
    /// when both before and after it's on the globe.</summary>
    public void ZoomAt(double factor, double x, double y, double width, double height)
    {
        var before = new MapView(this, width, height);
        bool had = before.Pick(x, y, out var p0);
        MetresPerPixel *= factor;
        Clamp(height);
        if (had)
            Drag(p0, x, y, width, height);
    }

    /// <summary>The farthest from the equator the camera looks, degrees: a tenth of a metre short of the pole, where
    /// north has no direction and north-up no meaning.</summary>
    public const double MaxLatitude = 90 - 1e-6;

    /// <summary>A left drag from (fromX, fromY) to (toX, toY): up and down the screen is north and south along the
    /// meridian, a pixel to a pixel's metres, stopping at <see cref="MaxLatitude"/> (so dragging back leaves a pole at
    /// once); across the screen turns the globe about its axis so the parallel under the cursor keeps up with it, or,
    /// within 50 px of a pole, as if it were 50 px out (near the pole that's a spin about it, never a wild one).</summary>
    public void Pan(double fromX, double fromY, double toX, double toY, double width, double height)
    {
        var (lat, lon) = Earth.LatLon(Target);
        double cursorLat = new MapView(this, width, height).Pick(fromX, fromY, out var under) ? Earth.LatLon(under).Lat : lat;
        double radius = Math.Max(Earth.Radius * Math.Cos(cursorLat * Math.PI / 180), 50 * MetresPerPixel);   // the parallel's, metres
        lat = Math.Clamp(lat + (toY - fromY) * MetresPerPixel / Earth.Radius * 180 / Math.PI, -MaxLatitude, MaxLatitude);
        lon -= (toX - fromX) * MetresPerPixel / radius * 180 / Math.PI;
        Target = Earth.Up(lat, lon);
    }

    /// <summary>Moves the camera so the globe's point <paramref name="grab"/> comes under (x, y): the zoom's. Solved by
    /// Newton's method on where the point projects, the step halved until it helps (the camera's north-up axes turn as
    /// it moves, and near the limb a small turn moves the point a lot), to a ten-thousandth of a pixel.</summary>
    public void Drag(Vec3d grab, double x, double y, double width, double height)
    {
        for (int i = 0; i < 12; i++)
        {
            if (!new MapView(this, width, height).Project(grab, out double gx, out double gy))
                return;
            double ex = x - gx, ey = y - gy;
            if (ex * ex + ey * ey < 1e-8)
                return;
            // how the point moves on screen as the target moves a pixel's angle east, then north
            double eps = MetresPerPixel / Earth.Radius;
            var t = Target.Normalized();
            var (east, north) = (Earth.East(t), Earth.North(t));
            if (!Moved(t + east * eps, grab, width, height, out double ax, out double ay)
                || !Moved(t + north * eps, grab, width, height, out double bx, out double by))
                return;
            double j11 = (ax - gx) / eps, j21 = (ay - gy) / eps, j12 = (bx - gx) / eps, j22 = (by - gy) / eps;
            double det = j11 * j22 - j12 * j21;
            if (Math.Abs(det) < 1e-30)
                return;
            double a = (ex * j22 - ey * j12) / det, b = (ey * j11 - ex * j21) / det;
            // the full step, or half of it until the point is in view and nearer the cursor (near the limb a full
            // step can carry it over the horizon)
            double err = ex * ex + ey * ey;
            bool better = false;
            for (int k = 0; k < 20 && !better; k++, a /= 2, b /= 2)
            {
                var next = (t + east * a + north * b).Normalized();
                if (Moved(next, grab, width, height, out double nx, out double ny) && (x - nx) * (x - nx) + (y - ny) * (y - ny) < err)
                {
                    Target = next;
                    better = true;
                }
            }
            if (!better)
                return;
        }
    }

    readonly bool Moved(Vec3d target, Vec3d p, double width, double height, out double x, out double y) =>
        new MapView(this with { Target = target.Normalized() }, width, height).Project(p, out x, out y);

    /// <summary><paramref name="v"/> turned by the rotation that takes unit <paramref name="from"/> to unit
    /// <paramref name="to"/> about their common perpendicular.</summary>
    public static Vec3d Rotate(Vec3d v, Vec3d from, Vec3d to)
    {
        var k = Vec3d.Cross(from, to);
        double s = k.Length, c = Vec3d.Dot(from, to);
        if (s < 1e-15)
            return v;
        k /= s;
        return v * c + Vec3d.Cross(k, v) * s + k * (Vec3d.Dot(k, v) * (1 - c));
    }
}

/// <summary>A camera resolved for a target's size: the eye and its axes in Earth-centred doubles, and projection both
/// ways, in pixels (x right, y down). Everything that must stay put on the globe at any zoom (the grid now, ships and
/// coastlines later) is placed through this in double, never in float on the GPU.</summary>
public readonly struct MapView
{
    public readonly Vec3d Eye, Right, Up, Forward;
    public readonly double Width, Height, TanHalfX, TanHalfY, Altitude, MetresPerPixel;

    public MapView(in MapCamera cam, double width, double height)
    {
        (Width, Height) = (Math.Max(width, 1), Math.Max(height, 1));
        TanHalfY = MapCamera.TanHalfY;
        TanHalfX = TanHalfY * Width / Height;
        MetresPerPixel = cam.MetresPerPixel;
        Altitude = cam.Altitude(Height);
        var up = cam.Target.Normalized();
        Eye = up * (Earth.Radius + Altitude);
        Forward = -up;
        Right = Earth.East(up);
        Up = Earth.North(up);
    }

    /// <summary>The pixel a point projects to. False when it's behind the camera or, for a point on the sea, on the
    /// far side of the globe.</summary>
    public bool Project(Vec3d p, out double x, out double y)
    {
        var v = p - Eye;
        double z = Vec3d.Dot(v, Forward);
        x = y = 0;
        if (z <= 0)
            return false;
        x = (Vec3d.Dot(v, Right) / (z * TanHalfX) + 1) * Width / 2;
        y = (1 - Vec3d.Dot(v, Up) / (z * TanHalfY)) * Height / 2;
        return Vec3d.Dot(p, Eye) >= Vec3d.Dot(p, p);   // the sea at p faces the eye
    }

    /// <summary>The unit ray from the eye through pixel (x, y).</summary>
    public Vec3d Ray(double x, double y) =>
        (Forward + Right * ((2 * x / Width - 1) * TanHalfX) + Up * ((1 - 2 * y / Height) * TanHalfY)).Normalized();

    /// <summary>The point on the sea under pixel (x, y), if the ray meets the globe. Solved in a form that stays exact
    /// close to the sea: |eye|² - R² is altitude·(2R + altitude), and the nearer root comes without cancellation.</summary>
    public bool Pick(double x, double y, out Vec3d hit)
    {
        var d = Ray(x, y);
        double b = Vec3d.Dot(Eye, d), c = Altitude * (2 * Earth.Radius + Altitude), disc = b * b - c;
        if (disc < 0 || b >= 0)
        {
            hit = default;
            return false;
        }
        double t = c / (-b + Math.Sqrt(disc));
        hit = (Eye + d * t).Normalized() * Earth.Radius;
        return true;
    }
}
