using System.Numerics;
using Fleetwright.Sim.Core;

namespace Fleetwright.Map;

/// <summary>The lat/lon grid (docs/visuals/grid.md): parallels and meridians on a ladder of levels, 90°, 10°, 1°, 10',
/// 1', 0.1', 0.01' (10:1 but for two steps; a minute of latitude is a nautical mile), every level that's far enough
/// apart on the screen drawn at once, each line in its coarsest level's look (<see cref="GridLook"/>): a smooth
/// function of the zoom, so zooming fades levels in and grows them to the major look instead of swapping grids.
/// Meridians are faded by their own spacing, the widest on the screen (at the latitude nearest the equator), so near
/// the poles they thin out a level before the parallels do. Computed and projected in double, so the lines hold still
/// at any zoom. Only what's in view is sampled: the whole globe when its limb shows, else the latitude and longitude
/// span of the screen's border (and every longitude if a pole is in view). Over the blend band
/// (<see cref="GridLook.Squares"/>) it gives the field to the <see cref="SquareGrid"/> and goes to the margin:
/// <see cref="BorderTicks"/> mark the same lines on the screen's edge, like a chart's, there and in the world view
/// (<see cref="WorldView"/>). Allocates nothing.</summary>
public static class Graticule
{
    /// <summary>The levels, coarsest first, in hundredths of a minute (1/6000°).</summary>
    static readonly long[] Units = [540_000, 60_000, 6_000, 1_000, 100, 10, 1];
    static readonly string[] Labels = ["90°", "10°", "1°", "10'", "1'", "0.1'", "0.01'"];

    public static int Levels => Units.Length;

    const double PerDegree = 6000;
    const long Circle = 360 * 6000, Pole = 90 * 6000;
    const double MetresPerDegree = Earth.Radius * Math.PI / 180;
    const int MaxLines = 600, BorderSamples = 16, MaxSamples = 720;
    const double SagittaPx = 0.5, Deg = Math.PI / 180;

    /// <summary>A level's spacing, degrees.</summary>
    public static double Step(int level) => Units[level] / PerDegree;

    /// <summary>A level's spacing as the legend shows it: 1°, 10', 0.1'.</summary>
    public static string Label(int level) => Labels[level];

    /// <summary>How far apart a level's parallels are, metres (its meridians: times the cosine of the latitude).</summary>
    public static double Metres(int level) => Step(level) * MetresPerDegree;

    /// <summary>The finest level whose lines, <paramref name="squeeze"/> times a parallel's spacing apart, are drawn at
    /// all; -1 for none.</summary>
    static int Finest(in MapView v, double squeeze)
    {
        int f = -1;
        for (int i = 0; i < Units.Length && GridLook.Px(Metres(i) * squeeze, v) >= GridLook.Hidden; i++)
            f = i;
        return f;
    }

    /// <summary>The level the eye reads as the major one: the finest parallels grown at least half way to the major
    /// look (the coarsest when none has).</summary>
    public static int Major(in MapView v)
    {
        int m = 0;
        for (int i = 0; i < Units.Length && GridLook.Weight(GridLook.Px(Metres(i), v)) >= 0.5; i++)
            m = i;
        return m;
    }

    /// <summary>The coarsest level a line belongs to, by its value in hundredths of a minute.</summary>
    static int LevelOf(long u)
    {
        u = (u % Circle + Circle) % Circle;
        for (int i = 0; i < Units.Length; i++)
            if (u % Units[i] == 0)
                return i;
        return Units.Length - 1;
    }

    static (Vector4 Color, float Width) Look(long u, bool parallel, in MapView v, double squeeze, float alpha) =>
        GridLook.Line(GridLook.Px(Metres(LevelOf(u)) * squeeze, v), alpha, parallel ? u == 0 : u % Circle == 0);

    /// <summary>The sampling step along a line, degrees of its own angle (longitude on a parallel, latitude on a
    /// meridian), so a chord strays at most half a pixel from the arc: a line's curvature on the screen is about
    /// (1 + tan|lat|) / R (the sphere's, and a parallel's own turning), and a parallel's angle is along a circle of
    /// R·cos(lat).</summary>
    static double SampleStep(in MapView v, double latDeg, bool parallel)
    {
        double lat = Math.Min(Math.Abs(latDeg), 89.9999) * Deg;
        double chord = Math.Sqrt(8 * SagittaPx * v.MetresPerPixel * Earth.Radius / (1 + Math.Tan(lat)));
        return Math.Clamp(chord / (Earth.Radius * (parallel ? Math.Cos(lat) : 1)) / Deg, 1e-7, 5);
    }

    /// <summary>How much the margin's ticks show: from the blend band on (the square grid in the field), and in the
    /// world view.</summary>
    public static double Margin(in MapView view) => Math.Max(GridLook.Squares(view), WorldView.Factor(view));

    public static void Build(in MapView view, MapLines lines)
    {
        float alpha = (float)((1 - WorldView.Factor(view)) * (1 - GridLook.Squares(view)));
        if (alpha <= 0)
            return;
        var (camLat, camLon) = Earth.LatLon(-view.Forward);
        if (!Covered(view))
        {
            // the limb shows: each line from where it meets the horizon (dot(p, centre) = cos a) to where it leaves
            double cosA = Earth.Radius / (Earth.Radius + view.Altitude);
            double near = Math.Max(0, Math.Abs(camLat) - Math.Acos(cosA) / Deg);
            double squeeze = Math.Cos(near * Deg), dsm = SampleStep(view, 0, false);
            int fp = Finest(view, 1), fm = Finest(view, squeeze);
            double sc = Math.Sin(camLat * Deg), cc = Math.Cos(camLat * Deg);
            if (fp >= 0)
            {
                long u = Units[fp];
                for (long k = -Pole / u; k <= Pole / u; k++)
                {
                    long val = k * u;
                    double lat = val / PerDegree, num = cosA - Math.Sin(lat * Deg) * sc, den = Math.Cos(lat * Deg) * cc;
                    if (Math.Abs(val) >= Pole || num > den)
                        continue;   // behind the horizon all round
                    double half = num <= -den ? 180 : Math.Acos(num / den) / Deg;
                    var (color, width) = Look(val, true, view, 1, alpha);
                    Parallel(view, lines, lat, camLon - half, camLon + half, color, width, SampleStep(view, lat, true));
                }
            }
            if (fm >= 0)
            {
                long u = Units[fm], k0 = (long)Math.Ceiling((camLon - 180) * PerDegree / u);
                for (long k = k0; k - k0 < Circle / u && k * u < (camLon + 180) * PerDegree - 1e-6; k++)
                {
                    long val = k * u;
                    double lon = val / PerDegree;
                    double a = cc * Math.Cos((lon - camLon) * Deg), r = Math.Sqrt(a * a + sc * sc);
                    if (r <= cosA)
                        continue;
                    double mid = Math.Atan2(sc, a) / Deg, half = Math.Acos(cosA / r) / Deg;
                    double lat0 = Math.Max(-90, mid - half), lat1 = Math.Min(90, mid + half);
                    var (color, width) = Look(val, false, view, squeeze, alpha);
                    if (lat1 > lat0)
                        Meridian(view, lines, lon, lat0, lat1, color, width, dsm);
                }
            }
            lines.End();
            return;
        }

        // close in: the span of the screen's border
        var (latMin, latMax, lonMin, lonMax) = Span(view, camLon);
        double sq = Squeeze(latMin, latMax);
        int fpc = Finest(view, 1), fmc = Finest(view, sq);
        if (fpc >= 0)
        {
            long u = Units[fpc], k0 = (long)Math.Ceiling(latMin * PerDegree / u);
            for (long k = k0; k - k0 < MaxLines && k * u <= latMax * PerDegree + 1e-6; k++)
            {
                long val = k * u;
                if (Math.Abs(val) >= Pole)
                    continue;
                double lat = val / PerDegree;
                var (color, width) = Look(val, true, view, 1, alpha);
                Parallel(view, lines, lat, lonMin, lonMax, color, width, SampleStep(view, lat, true));
            }
        }
        if (fmc >= 0)
        {
            double ds = SampleStep(view, Math.Max(Math.Abs(latMin), Math.Abs(latMax)), false);
            long u = Units[fmc], k0 = (long)Math.Ceiling(lonMin * PerDegree / u);
            for (long k = k0; k - k0 < MaxLines && k * u <= lonMax * PerDegree + 1e-6; k++)
            {
                long val = k * u;
                double lon = val / PerDegree;
                if (lon >= camLon + 180 - 1e-9 && lonMax - lonMin >= 360 - 1e-9)
                    continue;   // the full circle: the first meridian again
                var (color, width) = Look(val, false, view, sq, alpha);
                Meridian(view, lines, lon, Math.Max(latMin, -90), Math.Min(latMax, 90), color, width, ds);
            }
        }
        lines.End();
    }

    /// <summary>The meridians' spacing over the parallels': the cosine of the latitude nearest the equator in the span.</summary>
    static double Squeeze(double latMin, double latMax) =>
        Math.Cos((latMin > 0 ? latMin : latMax < 0 ? -latMax : 0) * Deg);

    /// <summary>The latitude and longitude span of the screen's border (longitudes about the camera's), taking in a
    /// pole on the screen.</summary>
    static (double LatMin, double LatMax, double LonMin, double LonMax) Span(in MapView view, double camLon)
    {
        double latMin = double.MaxValue, lonMin = double.MaxValue, latMax = double.MinValue, lonMax = double.MinValue;
        for (int k = 0; k <= BorderSamples; k++)
        {
            double f = (double)k / BorderSamples;
            Widen(view, f * view.Width, 0, camLon, ref latMin, ref latMax, ref lonMin, ref lonMax);
            Widen(view, f * view.Width, view.Height, camLon, ref latMin, ref latMax, ref lonMin, ref lonMax);
            Widen(view, 0, f * view.Height, camLon, ref latMin, ref latMax, ref lonMin, ref lonMax);
            Widen(view, view.Width, f * view.Height, camLon, ref latMin, ref latMax, ref lonMin, ref lonMax);
        }
        PoleIn(view, 90, camLon, ref latMin, ref latMax, ref lonMin, ref lonMax);
        PoleIn(view, -90, camLon, ref latMin, ref latMax, ref lonMin, ref lonMax);
        return (latMin, latMax, lonMin, lonMax);
    }

    /// <summary>A grid line where it meets the screen's edge: the point (target pixels), the edge (0 top, 1 right,
    /// 2 bottom, 3 left), whether it's a parallel, its latitude or longitude (degrees, longitude in -180..180), its
    /// level's spacing (degrees), and its level's look: how much it's drawn (<see cref="GridLook.Fade"/>), how far
    /// grown to the major look (<see cref="GridLook.Weight"/>) and how much its label shows (<see cref="GridLook.Label"/>).</summary>
    public struct BorderTick
    {
        public double X, Y, Value, Step;
        public float Fade, Weight, Label;
        public byte Edge;
        public bool Parallel;
    }

    const int EdgeSamples = 96;
    const float TickMinPx = 4, TickMaxPx = 12;
    static readonly Vector4 TickColor = new(0.85f, 0.92f, 1.0f, 0.65f);

    /// <summary>The grid's lines where they cross the screen's edge, into <paramref name="ticks"/> (cleared first),
    /// while the margin shows (<see cref="Margin"/>) and the screen is all globe; and their marks, faded in with it, longer and brighter as their level grows to the major look, into <paramref name="lines"/>. The
    /// top edge is <paramref name="topInsetPx"/> down (under the menu bar). Each edge is walked in short steps, every
    /// line crossed in a step found (a level's lines may be closer than a step).</summary>
    public static void BorderTicks(in MapView view, List<BorderTick> ticks, MapLines lines, double topInsetPx = 0)
    {
        ticks.Clear();
        float alpha = (float)Margin(view);
        if (alpha <= 0 || !Covered(view))
            return;
        var (_, camLon) = Earth.LatLon(-view.Forward);
        var (latMin, latMax, _, _) = Span(view, camLon);
        double squeeze = Squeeze(latMin, latMax), w = view.Width, h = view.Height, top = topInsetPx;
        int fp = Finest(view, 1), fm = Finest(view, squeeze);
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
                    Cross(ticks, view, e, true, lat0, lat, x0, y0, x, y, fp, 1);
                    Cross(ticks, view, e, false, lon0, lon, x0, y0, x, y, fm, squeeze);
                }
                (lat0, lon0, x0, y0, has) = (lat, lon, x, y, true);
            }
        }
        foreach (var t in ticks)
        {
            var (nx, ny) = t.Edge switch { 0 => (0.0, 1.0), 1 => (-1.0, 0.0), 2 => (0.0, -1.0), _ => (1.0, 0.0) };   // inward
            double len = TickMinPx + (TickMaxPx - TickMinPx) * t.Weight;
            lines.BeginStrip(TickColor with { W = TickColor.W * alpha * t.Fade * (0.6f + 0.4f * t.Weight) }, 1 + 0.4f * t.Weight);
            lines.Point(t.X, t.Y);
            lines.Point(t.X + nx * len, t.Y + ny * len);
            lines.End();
        }
    }

    /// <summary>Every line of level <paramref name="level"/> (and so of the coarser ones) between two samples of an
    /// edge, interpolated along the step.</summary>
    static void Cross(List<BorderTick> ticks, in MapView v, int edge, bool parallel, double a, double b,
        double x0, double y0, double x1, double y1, int level, double squeeze)
    {
        if (level < 0 || a == b)
            return;
        long u = Units[level];
        long k0 = (long)Math.Floor(Math.Min(a, b) * PerDegree / u) + 1, k1 = (long)Math.Floor(Math.Max(a, b) * PerDegree / u);
        for (long k = k0; k <= k1; k++)
        {
            long val = k * u;
            double deg = val / PerDegree, t = (deg - a) / (b - a);
            if (parallel && Math.Abs(val) >= Pole)
                continue;
            int l = LevelOf(val);
            double px = GridLook.Px(Metres(l) * squeeze, v);
            ticks.Add(new BorderTick
            {
                X = x0 + (x1 - x0) * t, Y = y0 + (y1 - y0) * t, Edge = (byte)edge, Parallel = parallel,
                Value = parallel ? deg : Math.IEEERemainder(deg, 360), Step = Step(l),
                Fade = (float)GridLook.Fade(px), Weight = (float)GridLook.Weight(px), Label = (float)GridLook.Label(px),
            });
        }
    }

    /// <summary>A line's latitude or longitude as a chart's margin shows it, to its level's spacing, in degrees and
    /// decimal minutes: 56°N, 56°40'N, 56°42.3'N, 56°42.31'N.</summary>
    public static string Format(double value, double step, bool parallel)
    {
        char hemi = parallel ? (value >= 0 ? 'N' : 'S') : (value >= 0 ? 'E' : 'W');
        long u = (long)Math.Round(Math.Abs(value) * PerDegree);
        long d = u / 6000, cm = u % 6000;
        const double eps = 1e-9;
        return step >= 1 - eps ? $"{d}°{hemi}"
            : step >= 1 / 60.0 - eps ? $"{d}°{cm / 100:00}'{hemi}"
            : step >= 1 / 600.0 - eps ? $"{d}°{cm / 100:00}.{cm / 10 % 10}'{hemi}"
            : $"{d}°{cm / 100:00}.{cm % 100:00}'{hemi}";
    }

    /// <summary>True when every corner of the screen is on the globe (then the whole screen is: its outline is convex).</summary>
    public static bool Covered(in MapView v) =>
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
    static void PoleIn(in MapView v, double pole, double camLon,
        ref double latMin, ref double latMax, ref double lonMin, ref double lonMax)
    {
        if (!v.Project(Earth.Position(pole, 0), out double x, out double y) || x < 0 || x > v.Width || y < 0 || y > v.Height)
            return;
        (latMin, latMax) = (Math.Min(latMin, pole), Math.Max(latMax, pole));
        (lonMin, lonMax) = (camLon - 180, camLon + 180);
    }

    /// <summary>A parallel from lon0 to lon1, sampled about every ds degrees by turning the last point about the axis
    /// (no trig per point), broken where it goes behind the globe.</summary>
    static void Parallel(in MapView v, MapLines lines, double lat, double lon0, double lon1, Vector4 color, float width, double ds)
    {
        if (color.W < GridLook.Faintest)
            return;
        int n = Math.Clamp((int)Math.Ceiling((lon1 - lon0) / ds), 1, MaxSamples);
        double d = (lon1 - lon0) / n * Deg, cd = Math.Cos(d), sd = Math.Sin(d);
        double r = Earth.Radius * Math.Cos(lat * Deg), z = Earth.Radius * Math.Sin(lat * Deg);
        double x = r * Math.Cos(lon0 * Deg), y = r * Math.Sin(lon0 * Deg);
        lines.BeginStrip(color, width);
        for (int i = 0; i <= n; i++)
        {
            Sample(v, lines, new Vec3d(x, y, z));
            (x, y) = (x * cd - y * sd, x * sd + y * cd);
        }
        lines.End();
    }

    /// <summary>A meridian from lat0 to lat1, sampled about every ds degrees by turning the last point in its plane.</summary>
    static void Meridian(in MapView v, MapLines lines, double lon, double lat0, double lat1, Vector4 color, float width, double ds)
    {
        if (color.W < GridLook.Faintest)
            return;
        int n = Math.Clamp((int)Math.Ceiling((lat1 - lat0) / ds), 1, MaxSamples);
        double d = (lat1 - lat0) / n * Deg, cd = Math.Cos(d), sd = Math.Sin(d);
        double mx = Earth.Radius * Math.Cos(lon * Deg), my = Earth.Radius * Math.Sin(lon * Deg);
        double c = Math.Cos(lat0 * Deg), s = Math.Sin(lat0 * Deg);
        lines.BeginStrip(color, width);
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
