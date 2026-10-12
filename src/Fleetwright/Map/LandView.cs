using System.Numerics;
using Fleetwright.Sim.Core;

namespace Fleetwright.Map;

/// <summary>The land as the map draws it, rebuilt on the CPU in double whenever the view changes: each ring near the
/// view is clipped to it, projected, and handed on as a fan of triangles for the fill (filled by winding count on the
/// GPU, so no triangulation) and as the coast's lines (<see cref="MapLines"/>).
/// <para>The clip is by planes through the Earth's centre, as a GPU clips against its frustum: an edge is a
/// great-circle arc, and where a ring leaves and re-enters, the join runs along the clip plane's great circle, off
/// screen. Close in, the planes pass just outside the screen's corners; when the limb shows, they're a polygon
/// inscribed in the horizon, so nothing behind the globe is drawn. Joins along a plane are fill only, never coast.
/// </para><para>The level of detail follows the zoom: 1:110M for the globe, 1:50M for a theatre, 1:10M closer in.
/// Allocates nothing once its buffers have grown to the views it's shown.</para></summary>
public sealed class LandView(Land coarse, Land medium, Land fine)
{
    struct Pt
    {
        public Vec3d P;
        public int Mask;   // the clip planes the point lies on (bits); 0 for the ring's own points
    }

    const int HorizonSides = 32;
    static readonly Vector4 CoastColor = new(0.86f, 0.80f, 0.60f, 0.55f);

    Pt[] a = new Pt[4096], b = new Pt[4096];
    readonly Vec3d[] planes = new Vec3d[HorizonSides];
    readonly Vec3d[] corners = new Vec3d[HorizonSides];
    Vector2[] fill = new Vector2[1 << 16];
    Vector2[] px = new Vector2[4096];
    int fillCount, planeCount;

    /// <summary>The fill's triangles in NDC, wound either way: the GPU counts windings.</summary>
    public ReadOnlySpan<Vector2> Fill => fill.AsSpan(0, fillCount);

    /// <summary>The land at the view's level of detail.</summary>
    public Land LevelFor(in MapView v)
    {
        double across = v.MetresPerPixel * v.Width;
        return across >= 6_000_000 ? coarse : across >= 600_000 ? medium : fine;
    }

    /// <summary>Rebuilds the fill and adds the coast to <paramref name="lines"/>.</summary>
    public void Build(in MapView v, MapLines lines)
    {
        fillCount = 0;
        var land = LevelFor(v);
        var centre = -v.Forward;
        Region(v, centre);
        for (int i = 0; i < land.RingCount; i++)
        {
            var (c, cos) = land.Cap(i);
            if (Clear(c, cos, centre))
                continue;
            int n = Clip(land, i, centre, Inside(c, cos, centre));
            if (n >= 3)
                Emit(v, n, lines);
        }
        lines.End();
    }

    /// <summary>Sets the clip planes for the view (inward normals through the centre) and the caps around and inside
    /// what they enclose.</summary>
    void Region(in MapView v, Vec3d centre)
    {
        // close in: the screen's corners, pushed 5% outward, all on the globe
        const double M = 0.05;
        if (v.Pick(-M * v.Width, -M * v.Height, out corners[0]) && v.Pick((1 + M) * v.Width, -M * v.Height, out corners[1])
            && v.Pick((1 + M) * v.Width, (1 + M) * v.Height, out corners[2]) && v.Pick(-M * v.Width, (1 + M) * v.Height, out corners[3]))
            planeCount = 4;
        else
        {
            // the limb shows: a polygon inside the horizon
            double cosA = Earth.Radius / (Earth.Radius + v.Altitude), sinA = Math.Sqrt(1 - cosA * cosA);
            var (e, n) = (Earth.East(centre), Earth.North(centre));
            for (int k = 0; k < HorizonSides; k++)
            {
                double t = 2 * Math.PI * k / HorizonSides;
                corners[k] = centre * cosA + (e * Math.Cos(t) + n * Math.Sin(t)) * sinA;
            }
            planeCount = HorizonSides;
        }
        // the cap around the clip region (through its farthest corner), and the one inside every plane
        (viewCos, innerSin) = (1, 1);
        for (int k = 0; k < planeCount; k++)
        {
            var p = Vec3d.Cross(corners[k], corners[(k + 1) % planeCount]).Normalized();
            planes[k] = Vec3d.Dot(p, centre) >= 0 ? p : -p;
            viewCos = Math.Min(viewCos, Vec3d.Dot(corners[k], centre));
            innerSin = Math.Min(innerSin, Vec3d.Dot(planes[k], centre));
        }
        viewSin = Math.Sqrt(Math.Max(0, 1 - viewCos * viewCos));
        innerCos = Math.Sqrt(Math.Max(0, 1 - innerSin * innerSin));
    }

    // the view's caps, as cosines and sines, so the tests below need no trig
    double viewCos, viewSin, innerCos, innerSin;

    /// <summary>Whether a cap (centre c, cos of its radius) is clear of the view's: the angle between the centres is
    /// over the two radii, by cos(r + v) = cos r cos v - sin r sin v.</summary>
    bool Clear(Vec3d c, double cos, Vec3d centre)
    {
        if (cos <= -viewCos)
            return false;   // the radii add up to half a turn or more
        double sin = Math.Sqrt(Math.Max(0, 1 - cos * cos));
        return Vec3d.Dot(c, centre) < cos * viewCos - sin * viewSin;
    }

    /// <summary>Whether a cap lies inside every clip plane (inside the inscribed cap), so its ring needs no clipping.</summary>
    bool Inside(Vec3d c, double cos, Vec3d centre)
    {
        if (cos < innerCos)
            return false;   // wider than the inscribed cap
        double sin = Math.Sqrt(Math.Max(0, 1 - cos * cos));
        return Vec3d.Dot(c, centre) > innerCos * cos + innerSin * sin;   // the angle is under (inner - r)
    }

    /// <summary>Clips ring i by every plane (Sutherland-Hodgman); the result is in a, its length returned. A chunk whose
    /// cap is clear of the view comes in as its first point only: the loop between it and that shortcut stays inside
    /// its cap, so it can't change what's in view, and a far coast costs a point per chunk.</summary>
    int Clip(Land land, int i, Vec3d centre, bool inside)
    {
        var ring = land.Ring(i);
        Grow(ref a, ring.Length * 2);
        int n = 0;
        for (int j = 0; j < land.ChunkCount(i); j++)
        {
            int from = j * Land.ChunkSize, to = Math.Min(from + Land.ChunkSize, ring.Length);
            var (c, cos) = land.ChunkCap(i, j);
            if (cos > 0 && Clear(c, cos, centre))
                to = from + 1;
            for (int k = from; k < to; k++)
                a[n++] = new Pt { P = ring[k] };
        }
        for (int k = 0; k < planeCount && n > 0 && !inside; k++)
        {
            Grow(ref b, n * 2);
            var plane = planes[k];
            int m = 0, bit = 1 << k;
            var s = a[n - 1];
            double ds = Vec3d.Dot(s.P, plane);
            for (int j = 0; j < n; j++)
            {
                var e = a[j];
                double de = Vec3d.Dot(e.P, plane);
                if ((de >= 0) != (ds >= 0))
                {
                    double t = ds / (ds - de);
                    b[m++] = new Pt { P = (s.P + (e.P - s.P) * t).Normalized(), Mask = (s.Mask & e.Mask) | bit };
                }
                if (de >= 0)
                    b[m++] = e;
                (s, ds) = (e, de);
            }
            (a, b) = (b, a);
            n = m;
        }
        return n;
    }

    /// <summary>Projects the clipped ring, fans it into the fill and draws its coast (not its joins along a plane).</summary>
    void Emit(in MapView v, int n, MapLines lines)
    {
        Grow(ref px, n);
        for (int k = 0; k < n; k++)
        {
            v.Project(a[k].P * Earth.Radius, out double x, out double y);
            px[k] = new Vector2((float)x, (float)y);
        }
        int need = fillCount + 3 * (n - 2);
        if (need > fill.Length)
            Array.Resize(ref fill, Math.Max(fill.Length * 2, need));
        var p0 = Ndc(v, px[0]);
        for (int k = 1; k < n - 1; k++)
        {
            fill[fillCount++] = p0;
            fill[fillCount++] = Ndc(v, px[k]);
            fill[fillCount++] = Ndc(v, px[k + 1]);
        }
        // the coast: every edge but those along a clip plane
        lines.BeginStrip(CoastColor, 1.2f);
        int start = 0;
        for (int k = 0; k < n; k++)
            if ((a[k].Mask & a[(k + 1) % n].Mask) != 0)
            {
                start = (k + 1) % n;   // begin after a join, so a strip never wraps across one
                break;
            }
        lines.Point(px[start].X, px[start].Y);
        for (int j = 1; j <= n; j++)
        {
            int prev = (start + j - 1) % n, k = (start + j) % n;
            if ((a[prev].Mask & a[k].Mask) != 0)
                lines.End();
            lines.Point(px[k].X, px[k].Y);
        }
        lines.End();
    }

    static Vector2 Ndc(in MapView v, Vector2 p) => new((float)(p.X / v.Width * 2 - 1), (float)(1 - p.Y / v.Height * 2));

    static void Grow<T>(ref T[] arr, int n)
    {
        if (arr.Length < n)
            Array.Resize(ref arr, Math.Max(arr.Length * 2, n));
    }
}
