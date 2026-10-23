using Fleetwright.Sim.Core;

namespace Fleetwright.Map;

/// <summary>The square grid at the battle's zooms (docs/visuals/grid.md): straight lines a round distance apart in the
/// map's unit (<see cref="MapUnits"/>; 1000, 100, 10, 1, 0.1, 0.01, exactly 10:1), on a gnomonic plane touching the
/// globe at an anchor, north up there: a point at (x, y) on the plane is normalize(R·A + x·east + y·north), so every
/// line is a great circle, a straight line on the sea, and within 100 nm of the anchor the squares are true to 0.3%.
/// Its levels fade as the graticule's do (<see cref="GridLook"/>). It takes over from the graticule over the blend band
/// (<see cref="GridLook.Squares"/>) and fades out into the world view like it.
///
/// The anchor is the whole-degree node nearest the view (so the 10-unit rows lie on the 10' parallels near it, in nm).
/// While the grid is faded out it follows the view, unseen; while it shows it holds, so the lines stay put on the sea,
/// until the grid's north is <see cref="GridLook.ReanchorDeg"/> off the view's or the view is
/// <see cref="GridLook.ReanchorNm"/> away; then the new anchor's grid fades in over the old one's in
/// <see cref="GridLook.ReanchorSeconds"/> of real time (a display clock: the grid isn't the sim). Allocates nothing.</summary>
public sealed class SquareGrid
{
    /// <summary>The levels, coarsest first, in hundredths of the unit.</summary>
    static readonly long[] Units = [100_000, 10_000, 1_000, 100, 10, 1];

    public static int Levels => Units.Length;

    const int MaxLines = 600, BorderSamples = 16, MaxSamples = 64;
    const double SagittaPx = 0.5, Deg = Math.PI / 180;

    /// <summary>A level's spacing, metres.</summary>
    public static double Metres(int level) => Units[level] / 100.0 * MapUnits.Metres;

    /// <summary>The level the eye reads as the major one: the finest grown at least half way to the major look.</summary>
    public static int Major(in MapView v)
    {
        int m = 0;
        for (int i = 0; i < Units.Length && GridLook.Weight(GridLook.Px(Metres(i), v)) >= 0.5; i++)
            m = i;
        return m;
    }

    public readonly struct Anchor
    {
        public readonly double Lat, Lon;
        public readonly Vec3d Up, East, North;

        public Anchor(double lat, double lon)
        {
            (Lat, Lon) = (lat, lon);
            Up = Earth.Up(lat, lon);
            (East, North) = (Earth.East(Up), Earth.North(Up));
        }
    }

    Anchor current, old;
    double currentAlpha, oldAlpha;
    bool has;

    /// <summary>The anchor the grid is drawn about now (the one fading in, during a fade).</summary>
    public Anchor Current => current;

    /// <summary>True while one anchor's grid fades into another's: the lines change with no camera change.</summary>
    public bool Fading => has && (currentAlpha < 1 || oldAlpha > 0);

    /// <summary>The whole-degree node nearest a point, its latitude within ±89° (east and north defined).</summary>
    public static Anchor Nearest(double lat, double lon) => new(Math.Clamp(Math.Round(lat), -89, 89), Math.Round(lon));

    /// <summary>True when a view centred at (lat, lon) has gone past the anchor's limits: the grid's north more than
    /// <see cref="GridLook.ReanchorDeg"/> off the view's (Δlon·sin(lat), the meridians' convergence), or the view
    /// more than <see cref="GridLook.ReanchorNm"/> away.</summary>
    public static bool Past(in Anchor a, double lat, double lon)
    {
        double dLon = Math.IEEERemainder(lon - a.Lon, 360);
        if (Math.Abs(dLon * Math.Sin(lat * Deg)) > GridLook.ReanchorDeg)
            return true;
        return Earth.Distance(Earth.Position(lat, lon), Earth.Position(a.Lat, a.Lon)) > GridLook.ReanchorNm * MapUnits.MetresPerNauticalMile;
    }

    /// <summary>The anchor for this frame, <paramref name="dt"/> real seconds on: free while the grid is faded out,
    /// held while it shows, a fade to a new one past the limits (one fading out already is dropped).</summary>
    public void Update(in MapView view, double dt)
    {
        var (lat, lon) = Earth.LatLon(-view.Forward);
        if (!has || GridLook.Squares(view) * (1 - WorldView.Factor(view)) <= 0)
        {
            (current, currentAlpha, oldAlpha, has) = (Nearest(lat, lon), 1, 0, true);
            return;
        }
        double step = GridLook.ReanchorSeconds > 0 ? dt / GridLook.ReanchorSeconds : 1;
        (currentAlpha, oldAlpha) = (Math.Min(1, currentAlpha + step), Math.Max(0, oldAlpha - step));
        if (Past(current, lat, lon))
            (old, oldAlpha, current, currentAlpha) = (current, currentAlpha, Nearest(lat, lon), GridLook.ReanchorSeconds > 0 ? 0 : 1);
    }

    public void Build(in MapView view, MapLines lines)
    {
        float alpha = (float)(GridLook.Squares(view) * (1 - WorldView.Factor(view)));
        if (alpha <= 0 || !has || !Graticule.Covered(view))
            return;
        if (oldAlpha > 0)
            Build(view, lines, old, (float)(alpha * oldAlpha));
        Build(view, lines, current, (float)(alpha * currentAlpha));
        lines.End();
    }

    /// <summary>A point on the plane of <paramref name="a"/> (metres east and north of it) on the sea.</summary>
    public static Vec3d OnSea(in Anchor a, double x, double y) => (a.Up * Earth.Radius + a.East * x + a.North * y).Normalized() * Earth.Radius;

    /// <summary>A point on the sea on the plane of <paramref name="a"/>; false on the far half of the globe.</summary>
    public static bool OnPlane(in Anchor a, Vec3d p, out double x, out double y)
    {
        double d = Vec3d.Dot(p, a.Up);
        x = y = 0;
        if (d <= 0)
            return false;
        (x, y) = (Earth.Radius * Vec3d.Dot(p, a.East) / d, Earth.Radius * Vec3d.Dot(p, a.North) / d);
        return true;
    }

    static void Build(in MapView view, MapLines lines, in Anchor a, float alpha)
    {
        if (alpha <= 0)
            return;
        // the screen's border on the plane
        double xMin = double.MaxValue, yMin = double.MaxValue, xMax = double.MinValue, yMax = double.MinValue;
        for (int k = 0; k <= BorderSamples; k++)
        {
            double f = (double)k / BorderSamples;
            Widen(view, a, f * view.Width, 0, ref xMin, ref xMax, ref yMin, ref yMax);
            Widen(view, a, f * view.Width, view.Height, ref xMin, ref xMax, ref yMin, ref yMax);
            Widen(view, a, 0, f * view.Height, ref xMin, ref xMax, ref yMin, ref yMax);
            Widen(view, a, view.Width, f * view.Height, ref xMin, ref xMax, ref yMin, ref yMax);
        }
        if (xMin > xMax || yMin > yMax)
            return;
        int fine = -1;
        for (int i = 0; i < Units.Length && GridLook.Px(Metres(i), view) >= GridLook.Hidden; i++)
            fine = i;
        if (fine < 0)
            return;
        // a great circle's chord strays half a pixel at most
        double chord = Math.Sqrt(8 * SagittaPx * view.MetresPerPixel * Earth.Radius), step = Metres(fine);
        long u = Units[fine], k0 = (long)Math.Ceiling(xMin / step), j0 = (long)Math.Ceiling(yMin / step);
        int nAcross = Math.Clamp((int)Math.Ceiling((yMax - yMin) / chord), 1, MaxSamples);
        for (long k = k0; k - k0 < MaxLines && k * step <= xMax; k++)
            Line(view, lines, a, k * step, yMin, k * step, yMax, nAcross, Look(k * u, view, alpha));
        nAcross = Math.Clamp((int)Math.Ceiling((xMax - xMin) / chord), 1, MaxSamples);
        for (long j = j0; j - j0 < MaxLines && j * step <= yMax; j++)
            Line(view, lines, a, xMin, j * step, xMax, j * step, nAcross, Look(j * u, view, alpha));
    }

    static (System.Numerics.Vector4 Color, float Width) Look(long value, in MapView v, float alpha)
    {
        value = Math.Abs(value);
        int level = Units.Length - 1;
        for (int i = 0; i < Units.Length; i++)
            if (value % Units[i] == 0)
            {
                level = i;
                break;
            }
        return GridLook.Line(GridLook.Px(Metres(level), v), alpha);
    }

    static void Line(in MapView v, MapLines lines, in Anchor a, double x0, double y0, double x1, double y1, int n,
        (System.Numerics.Vector4 Color, float Width) look)
    {
        if (look.Color.W < GridLook.Faintest)
            return;
        lines.BeginStrip(look.Color, look.Width);
        for (int i = 0; i <= n; i++)
        {
            double t = (double)i / n;
            if (v.Project(OnSea(a, x0 + (x1 - x0) * t, y0 + (y1 - y0) * t), out double px, out double py))
                lines.Point(px, py);
            else
                lines.End();
        }
        lines.End();
    }

    static void Widen(in MapView v, in Anchor a, double sx, double sy, ref double xMin, ref double xMax, ref double yMin, ref double yMax)
    {
        if (!v.Pick(sx, sy, out var p) || !OnPlane(a, p, out double x, out double y))
            return;
        (xMin, xMax) = (Math.Min(xMin, x), Math.Max(xMax, x));
        (yMin, yMax) = (Math.Min(yMin, y), Math.Max(yMax, y));
    }
}
