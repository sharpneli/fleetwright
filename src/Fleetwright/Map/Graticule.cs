using System.Numerics;
using Fleetwright.Sim.Core;

namespace Fleetwright.Map;

/// <summary>The lat/lon grid: parallels and meridians at a spacing the zoom picks (from 30° down to 1"), computed and
/// projected in double, so the lines hold still at any zoom. Only what's in view is sampled: the whole globe when its
/// limb shows, else the latitude and longitude span of the screen's border (and every longitude if a pole is in
/// view). The grid belongs to the planning table: it fades out into the world view (<see cref="WorldView"/>), where
/// <see cref="BorderTicks"/> mark the same lines on the screen's edge instead, like a chart's margin. Allocates
/// nothing.</summary>
public static class Graticule
{
    static readonly double[] Steps =
        [30, 10, 5, 2, 1, 0.5, 1 / 6.0, 1 / 12.0, 1 / 30.0, 1 / 60.0, 1 / 120.0, 1 / 360.0, 1 / 720.0, 1 / 1800.0, 1 / 3600.0];

    const double MetresPerDegree = Earth.Radius * Math.PI / 180;
    const double MinSpacingPx = 90;
    const int MaxLines = 200, SegmentsAcross = 64, BorderSamples = 16, MaxSamples = 720;
    const double SagittaPx = 0.25, Deg = Math.PI / 180;

    static readonly Vector4 LineColor = new(0.75f, 0.85f, 0.95f, 0.20f);
    static readonly Vector4 ZeroColor = new(0.85f, 0.92f, 1.0f, 0.42f);

    /// <summary>The spacing for a zoom, degrees: the finest whose lines are at least ~90 px apart.</summary>
    public static double Spacing(double metresPerPixel)
    {
        double best = Steps[0];
        foreach (double s in Steps)
            if (s * MetresPerDegree / metresPerPixel >= MinSpacingPx)
                best = s;
        return best;
    }

    /// <summary>Shows the spacing as degrees, minutes or seconds.</summary>
    public static string Label(double deg) =>
        deg >= 1 ? $"{deg:0.#}°" : deg >= 1 / 60.0 - 1e-12 ? $"{deg * 60:0.#}'" : $"{deg * 3600:0.#}\"";

    public static void Build(in MapView view, MapLines lines)
    {
        float alpha = (float)(1 - WorldView.Factor(view));
        if (alpha <= 0)
            return;
        double step = Spacing(view.MetresPerPixel);
        var (camLat, camLon) = Earth.LatLon(-view.Forward);
        if (!Covered(view))
        {
            // the limb shows: each line from where it meets the horizon (dot(p, centre) = cos a) to where it leaves
            double cosA = Earth.Radius / (Earth.Radius + view.Altitude);
            // every line is a circle of at most the globe's radius on screen (R / mpp pixels): a step of
            // sqrt(8 s mpp / R) radians keeps its chord within s pixels of the arc
            double ds = Math.Clamp(Math.Sqrt(8 * SagittaPx * view.MetresPerPixel / Earth.Radius) / Deg, 0.05, 5);
            double sc = Math.Sin(camLat * Deg), cc = Math.Cos(camLat * Deg);
            for (int k = (int)Math.Ceiling(-90 / step); k * step < 90 && k <= MaxLines; k++)
            {
                double lat = k * step, num = cosA - Math.Sin(lat * Deg) * sc, den = Math.Cos(lat * Deg) * cc;
                if (Math.Abs(lat) >= 90 || num > den)
                    continue;   // behind the horizon all round
                double half = num <= -den ? 180 : Math.Acos(num / den) / Deg;
                Parallel(view, lines, lat, camLon - half, camLon + half, k == 0, alpha, ds);
            }
            double first = Math.Ceiling((camLon - 180) / step) * step;
            for (int k = 0; k < MaxLines && first + k * step < camLon + 180 - 1e-9; k++)
            {
                double lon = first + k * step;
                double a = cc * Math.Cos((lon - camLon) * Deg), r = Math.Sqrt(a * a + sc * sc);
                if (r <= cosA)
                    continue;
                double mid = Math.Atan2(sc, a) / Deg, half = Math.Acos(cosA / r) / Deg;
                double lat0 = Math.Max(-90, mid - half), lat1 = Math.Min(90, mid + half);
                if (lat1 > lat0)
                    Meridian(view, lines, lon, lat0, lat1, Math.Abs(Math.IEEERemainder(lon, 360)) < 1e-9, alpha, ds);
            }
            lines.End();
            return;
        }

        // close in: the span of the screen's border
        double latMin = double.MaxValue, lonMin = double.MaxValue, latMax = double.MinValue, lonMax = double.MinValue;
        for (int k = 0; k <= BorderSamples; k++)
        {
            double f = (double)k / BorderSamples;
            Widen(view, f * view.Width, 0, camLon, ref latMin, ref latMax, ref lonMin, ref lonMax);
            Widen(view, f * view.Width, view.Height, camLon, ref latMin, ref latMax, ref lonMin, ref lonMax);
            Widen(view, 0, f * view.Height, camLon, ref latMin, ref latMax, ref lonMin, ref lonMax);
            Widen(view, view.Width, f * view.Height, camLon, ref latMin, ref latMax, ref lonMin, ref lonMax);
        }
        Pole(view, 90, camLon, ref latMin, ref latMax, ref lonMin, ref lonMax);
        Pole(view, -90, camLon, ref latMin, ref latMax, ref lonMin, ref lonMax);
        double dsc = Math.Max(latMax - latMin, lonMax - lonMin) / SegmentsAcross;
        double f0 = Math.Ceiling(latMin / step) * step;
        for (int k = 0; k < MaxLines && f0 + k * step <= latMax + 1e-12; k++)
        {
            double lat = f0 + k * step;
            if (Math.Abs(lat) < 90)
                Parallel(view, lines, lat, lonMin, lonMax, Math.Abs(lat) < 1e-9, alpha, dsc);
        }
        f0 = Math.Ceiling(lonMin / step) * step;
        for (int k = 0; k < MaxLines && f0 + k * step <= lonMax + 1e-12; k++)
        {
            double lon = f0 + k * step;
            if (lon >= camLon + 180 - 1e-9 && lonMax - lonMin >= 360 - 1e-9)
                continue;   // the full circle: the first meridian again
            Meridian(view, lines, lon, Math.Max(latMin, -90), Math.Min(latMax, 90), Math.Abs(Math.IEEERemainder(lon, 360)) < 1e-9, alpha, dsc);
        }
        lines.End();
    }

    /// <summary>A grid line where it meets the screen's edge: the point (target pixels), the edge (0 top, 1 right,
    /// 2 bottom, 3 left), whether it's a parallel, and its latitude or longitude (degrees, longitude in -180..180).</summary>
    public struct BorderTick
    {
        public double X, Y, Value;
        public byte Edge;
        public bool Parallel;
    }

    const int EdgeSamples = 96;
    const float TickPx = 10;
    static readonly Vector4 TickColor = new(0.85f, 0.92f, 1.0f, 0.65f);

    /// <summary>The grid's lines where they cross the screen's edge, into <paramref name="ticks"/> (cleared first),
    /// while the world view shows (<see cref="WorldView"/>) and the screen is all globe; and their marks, faded in with
    /// the world view, into <paramref name="lines"/>. The top edge is <paramref name="topInsetPx"/> down (under the
    /// menu bar). Each edge is walked in steps far finer than the grid's spacing, so a step crosses a line at most
    /// once.</summary>
    public static void BorderTicks(in MapView view, List<BorderTick> ticks, MapLines lines, double topInsetPx = 0)
    {
        ticks.Clear();
        float alpha = (float)WorldView.Factor(view);
        if (alpha <= 0 || !Covered(view))
            return;
        double step = Spacing(view.MetresPerPixel), w = view.Width, h = view.Height, top = topInsetPx;
        var (_, camLon) = Earth.LatLon(-view.Forward);
        for (int e = 0; e < 4; e++)
        {
            double lat0 = 0, lon0 = 0, x0 = 0, y0 = 0;
            bool has = false;
            for (int k = 0; k <= EdgeSamples; k++)
            {
                double f = (double)k / EdgeSamples;
                var (x, y) = e switch { 0 => (f * w, top), 1 => (w, top + f * (h - top)), 2 => (f * w, h), _ => (0.0, top + f * (h - top)) };
                if (!view.Pick(x, y, out var p))
                {
                    has = false;
                    continue;
                }
                var (lat, lon) = Earth.LatLon(p);
                lon = camLon + Math.IEEERemainder(lon - camLon, 360);
                if (has)
                {
                    Cross(ticks, e, true, lat0, lat, x0, y0, x, y, step);
                    Cross(ticks, e, false, lon0, lon, x0, y0, x, y, step);
                }
                (lat0, lon0, x0, y0, has) = (lat, lon, x, y, true);
            }
        }
        var color = Faded(TickColor, alpha);
        foreach (var t in ticks)
        {
            var (nx, ny) = t.Edge switch { 0 => (0.0, 1.0), 1 => (-1.0, 0.0), 2 => (0.0, -1.0), _ => (1.0, 0.0) };   // inward
            lines.BeginStrip(color, 1.2f);
            lines.Point(t.X, t.Y);
            lines.Point(t.X + nx * TickPx, t.Y + ny * TickPx);
            lines.End();
        }
    }

    static void Cross(List<BorderTick> ticks, int edge, bool parallel, double a, double b, double x0, double y0, double x1, double y1, double step)
    {
        double ka = Math.Floor(a / step), kb = Math.Floor(b / step);
        if (ka == kb)
            return;
        double v = Math.Max(ka, kb) * step, t = (v - a) / (b - a);
        ticks.Add(new BorderTick
        {
            X = x0 + (x1 - x0) * t, Y = y0 + (y1 - y0) * t, Edge = (byte)edge, Parallel = parallel,
            Value = parallel ? v : Math.IEEERemainder(v, 360),
        });
    }

    /// <summary>A line's latitude or longitude as a chart's margin shows it, to the grid's spacing: 56°N, 56°30'N,
    /// 56°30'10"N.</summary>
    public static string Format(double value, double step, bool parallel)
    {
        char hemi = parallel ? (value >= 0 ? 'N' : 'S') : (value >= 0 ? 'E' : 'W');
        long s = (long)Math.Round(Math.Abs(value) * 3600);
        long d = s / 3600, m = s / 60 % 60, sec = s % 60;
        return step >= 1 - 1e-9 ? $"{d}°{hemi}" : step >= 1 / 60.0 - 1e-12 ? $"{d}°{m:00}'{hemi}" : $"{d}°{m:00}'{sec:00}\"{hemi}";
    }

    static Vector4 Faded(Vector4 c, float alpha) => c with { W = c.W * alpha };

    /// <summary>True when every corner of the screen is on the globe (then the whole screen is: its outline is convex).</summary>
    static bool Covered(in MapView v) =>
        v.Pick(0, 0, out _) && v.Pick(v.Width, 0, out _) && v.Pick(0, v.Height, out _) && v.Pick(v.Width, v.Height, out _);

    static void Widen(in MapView v, double x, double y, double camLon,
        ref double latMin, ref double latMax, ref double lonMin, ref double lonMax)
    {
        if (!v.Pick(x, y, out var p))
            return;
        var (lat, lon) = Earth.LatLon(p);
        lon = camLon + Math.IEEERemainder(lon - camLon, 360);
        (latMin, latMax) = (Math.Min(latMin, lat), Math.Max(latMax, lat));
        (lonMin, lonMax) = (Math.Min(lonMin, lon), Math.Max(lonMax, lon));
    }

    /// <summary>A pole on the screen: the span reaches it, and takes in every longitude.</summary>
    static void Pole(in MapView v, double pole, double camLon,
        ref double latMin, ref double latMax, ref double lonMin, ref double lonMax)
    {
        if (!v.Project(Earth.Position(pole, 0), out double x, out double y) || x < 0 || x > v.Width || y < 0 || y > v.Height)
            return;
        (latMin, latMax) = (Math.Min(latMin, pole), Math.Max(latMax, pole));
        (lonMin, lonMax) = (camLon - 180, camLon + 180);
    }

    /// <summary>A parallel from lon0 to lon1, sampled every ds degrees by turning the last point about the axis (no
    /// trig per point), broken where it goes behind the globe.</summary>
    static void Parallel(in MapView v, MapLines lines, double lat, double lon0, double lon1, bool zero, float alpha, double ds)
    {
        int n = Math.Clamp((int)Math.Ceiling((lon1 - lon0) / ds), 1, MaxSamples);
        double d = (lon1 - lon0) / n * Deg, cd = Math.Cos(d), sd = Math.Sin(d);
        double r = Earth.Radius * Math.Cos(lat * Deg), z = Earth.Radius * Math.Sin(lat * Deg);
        double x = r * Math.Cos(lon0 * Deg), y = r * Math.Sin(lon0 * Deg);
        lines.BeginStrip(Faded(zero ? ZeroColor : LineColor, alpha), zero ? 1.5f : 1f);
        for (int i = 0; i <= n; i++)
        {
            Sample(v, lines, new Vec3d(x, y, z));
            (x, y) = (x * cd - y * sd, x * sd + y * cd);
        }
        lines.End();
    }

    /// <summary>A meridian from lat0 to lat1, sampled every ds degrees by turning the last point in its plane.</summary>
    static void Meridian(in MapView v, MapLines lines, double lon, double lat0, double lat1, bool zero, float alpha, double ds)
    {
        int n = Math.Clamp((int)Math.Ceiling((lat1 - lat0) / ds), 1, MaxSamples);
        double d = (lat1 - lat0) / n * Deg, cd = Math.Cos(d), sd = Math.Sin(d);
        double mx = Earth.Radius * Math.Cos(lon * Deg), my = Earth.Radius * Math.Sin(lon * Deg);
        double c = Math.Cos(lat0 * Deg), s = Math.Sin(lat0 * Deg);
        lines.BeginStrip(Faded(zero ? ZeroColor : LineColor, alpha), zero ? 1.5f : 1f);
        for (int i = 0; i <= n; i++)
        {
            Sample(v, lines, new Vec3d(mx * c, my * c, Earth.Radius * s));
            (c, s) = (c * cd - s * sd, s * cd + c * sd);
        }
        lines.End();
    }

    static void Sample(in MapView v, MapLines lines, Vec3d p)
    {
        if (v.Project(p, out double x, out double y))
            lines.Point(x, y);
        else
            lines.End();
    }
}
