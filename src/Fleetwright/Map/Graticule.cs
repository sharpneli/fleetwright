using System.Numerics;
using Fleetwright.Sim.Core;

namespace Fleetwright.Map;

/// <summary>The lat/lon grid: parallels and meridians at a spacing the zoom picks (from 30° down to 1"), computed and
/// projected in double, so the lines hold still at any zoom. Only what's in view is sampled: the whole globe when its
/// limb shows, else the latitude and longitude span of the screen's border (and every longitude if a pole is in
/// view). Allocates nothing.</summary>
public static class Graticule
{
    static readonly double[] Steps =
        [30, 10, 5, 2, 1, 0.5, 1 / 6.0, 1 / 12.0, 1 / 30.0, 1 / 60.0, 1 / 120.0, 1 / 360.0, 1 / 720.0, 1 / 1800.0, 1 / 3600.0];

    const double MetresPerDegree = Earth.Radius * Math.PI / 180;
    const double MinSpacingPx = 90;
    const int MaxLines = 200, SegmentsAcross = 64, BorderSamples = 16;

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
        double step = Spacing(view.MetresPerPixel);
        var (camLat, camLon) = Earth.LatLon(-view.Forward);
        double latMin = -90, latMax = 90, lonMin = camLon - 180, lonMax = camLon + 180, ds = 1;
        if (Covered(view))
        {
            latMin = lonMin = double.MaxValue;
            latMax = lonMax = double.MinValue;
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
            ds = Math.Max(latMax - latMin, lonMax - lonMin) / SegmentsAcross;
        }

        // parallels, then meridians
        double first = Math.Ceiling(latMin / step) * step;
        for (int k = 0; k < MaxLines && first + k * step <= latMax + 1e-12; k++)
        {
            double lat = first + k * step;
            if (Math.Abs(lat) >= 90)
                continue;
            Line(view, lines, lat, lonMin, lat, lonMax, Math.Abs(lat) < 1e-9, ds);
        }
        first = Math.Ceiling(lonMin / step) * step;
        for (int k = 0; k < MaxLines && first + k * step <= lonMax + 1e-12; k++)
        {
            double lon = first + k * step;
            if (lon >= camLon + 180 - 1e-9 && lonMax - lonMin >= 360 - 1e-9)
                continue;   // the full circle: the first meridian again
            double rem = Math.IEEERemainder(lon, 360);
            Line(view, lines, Math.Max(latMin, -90), lon, Math.Min(latMax, 90), lon, Math.Abs(rem) < 1e-9, ds);
        }
        lines.End();
    }

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

    /// <summary>A line of constant latitude or longitude from (lat0, lon0) to (lat1, lon1), sampled every ds degrees,
    /// broken where it goes behind the globe.</summary>
    static void Line(in MapView v, MapLines lines, double lat0, double lon0, double lat1, double lon1, bool zero, double ds)
    {
        double span = Math.Max(Math.Abs(lat1 - lat0), Math.Abs(lon1 - lon0));
        int n = Math.Clamp((int)Math.Ceiling(span / ds), 1, 720);
        lines.BeginStrip(zero ? ZeroColor : LineColor, zero ? 1.5f : 1f);
        for (int i = 0; i <= n; i++)
        {
            double f = (double)i / n;
            if (v.Project(Earth.Position(lat0 + (lat1 - lat0) * f, lon0 + (lon1 - lon0) * f), out double x, out double y))
                lines.Point(x, y);
            else
                lines.End();
        }
        lines.End();
    }
}
