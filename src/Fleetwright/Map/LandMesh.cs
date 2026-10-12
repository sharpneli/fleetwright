using System.Numerics;
using System.Runtime.InteropServices;
using Fleetwright.Sim.Core;

namespace Fleetwright.Map;

/// <summary>A vertex of the baked coast: the point, its neighbours along the ring and a side (±1). The vertex shader
/// projects all three and mitres the join in screen space, as <see cref="MapLines"/> does on the CPU.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct CoastVertex
{
    public Vector3 Prev, Cur, Next;
    public float Side;

    public const uint SizeInBytes = 40;
}

/// <summary>A level of the land baked once into static geometry, drawn with no CPU per frame (docs/battle/plan.md
/// step 1.4): the fill as fans of triangles for the winding count, and the coast as a line mesh.
/// <para>No triangulation: the winding-count fill takes any fans, as long as no triangle is big enough for its sag
/// below the sphere (L²/8R) to show. So the land is cut into tiles of a cube-sphere grid (six faces, each
/// <see cref="TilesPerFace"/> squared in its gnomonic plane, where great circles are straight) and each tile's piece
/// becomes a fan of its own. Per face, a ring is brought inside a 60° cap around the face's centre
/// (<see cref="CapRetraction"/>: inside a hemisphere, so the projection holds), projected, clipped to the face, then split
/// into quadrants down to the tiles (Sutherland-Hodgman), which keeps the winding number of every point inside a tile
/// even for the non-simple polygons the retraction makes. A tile inside a continent is a square: two triangles. Pieces
/// with no area are dropped.</para>
/// <para>Tiles are at most ~100 km a side (2/128 rad at a face's centre), so a fan's triangle, at most a tile's
/// diagonal, sags under 0.4 km: well inside half a pixel at the zooms the coarse levels are drawn at (1.37 x mpp
/// allows ~1 km at 760 m/px), and inside the 2 km the far-side depth test allows for. Positions are floats in metres,
/// Earth-centred: ~0.5 m, far below a pixel at these levels.</para></summary>
public sealed class LandMesh
{
    /// <summary>Tiles along a cube face's side: a power of two, as the face is split in quadrants.</summary>
    public const int TilesPerFace = 128;

    const int Depth = 7;   // 2^7 = TilesPerFace
    static readonly double FaceCapCos = Math.Cos(60 * Math.PI / 180);

    /// <summary>The fill: triangles (three vertices each), Earth-centred metres.</summary>
    public Vector3[] Fill { get; }

    /// <summary>The coast: two vertices per ring point and an index list of quads (two triangles each).</summary>
    public CoastVertex[] Coast { get; }
    public uint[] CoastIndices { get; }

    LandMesh(Vector3[] fill, CoastVertex[] coast, uint[] indices) => (Fill, Coast, CoastIndices) = (fill, coast, indices);

    // the cube's faces: the centre's axis and the gnomonic plane's two axes
    static readonly (Vec3d F, Vec3d U, Vec3d V)[] Faces =
    [
        (Vec3d.UnitX, Vec3d.UnitY, Vec3d.UnitZ), (-Vec3d.UnitX, -Vec3d.UnitY, Vec3d.UnitZ),
        (Vec3d.UnitY, -Vec3d.UnitX, Vec3d.UnitZ), (-Vec3d.UnitY, Vec3d.UnitX, Vec3d.UnitZ),
        (Vec3d.UnitZ, Vec3d.UnitY, -Vec3d.UnitX), (-Vec3d.UnitZ, Vec3d.UnitY, Vec3d.UnitX),
    ];

    public static LandMesh Bake(Land land)
    {
        var fill = new List<Vector3>();
        var cap = new CapRetraction();
        var poly = new List<(double X, double Y)>();
        foreach (var (f, u, v) in Faces)
        {
            cap.Set(f, FaceCapCos);
            for (int i = 0; i < land.RingCount; i++)
            {
                var (c, cos) = land.Cap(i);
                if (cap.Clear(c, cos))
                    continue;
                int n = cap.Retract(land, i);
                if (n < 3)
                    continue;
                poly.Clear();
                var pts = cap.Points;
                for (int k = 0; k < n; k++)
                {
                    var p = pts[k].P;
                    double w = Vec3d.Dot(p, f);
                    poly.Add((Vec3d.Dot(p, u) / w, Vec3d.Dot(p, v) / w));
                }
                Split(poly, -1, -1, 2, 0, f, u, v, fill);
            }
        }
        var (coast, indices) = CoastMesh(land);
        return new LandMesh([.. fill], coast, indices);
    }

    /// <summary>Clips a polygon (gnomonic coordinates on one face) to the square at (x0, y0) of side s, and either fans
    /// it (at tile size) or splits the result in four.</summary>
    static void Split(List<(double X, double Y)> poly, double x0, double y0, double s, int depth,
        Vec3d f, Vec3d u, Vec3d v, List<Vector3> fill)
    {
        var clipped = Clip(poly, x0, y0, x0 + s, y0 + s);
        if (clipped.Count < 3 || Math.Abs(Area(clipped)) < 1e-9 * s * s)
            return;
        if (depth == Depth)
        {
            var p0 = Point(clipped[0], f, u, v);
            for (int k = 1; k + 1 < clipped.Count; k++)
            {
                fill.Add(p0);
                fill.Add(Point(clipped[k], f, u, v));
                fill.Add(Point(clipped[k + 1], f, u, v));
            }
            return;
        }
        double h = s / 2;
        Split(clipped, x0, y0, h, depth + 1, f, u, v, fill);
        Split(clipped, x0 + h, y0, h, depth + 1, f, u, v, fill);
        Split(clipped, x0, y0 + h, h, depth + 1, f, u, v, fill);
        Split(clipped, x0 + h, y0 + h, h, depth + 1, f, u, v, fill);
    }

    static Vector3 Point((double X, double Y) g, Vec3d f, Vec3d u, Vec3d v)
    {
        var p = (f + u * g.X + v * g.Y).Normalized() * Earth.Radius;
        return new Vector3((float)p.X, (float)p.Y, (float)p.Z);
    }

    /// <summary>Sutherland-Hodgman against an axis-aligned rectangle: the four sides in turn.</summary>
    static List<(double X, double Y)> Clip(List<(double X, double Y)> poly, double x0, double y0, double x1, double y1)
    {
        var a = poly;
        a = ClipSide(a, p => p.X - x0, x0, true);
        a = ClipSide(a, p => x1 - p.X, x1, true);
        a = ClipSide(a, p => p.Y - y0, y0, false);
        a = ClipSide(a, p => y1 - p.Y, y1, false);
        return a;
    }

    /// <summary>Keeps the part of the polygon where <paramref name="inside"/> is not negative; the crossings land
    /// exactly on the line (x = at, or y = at), so neighbouring tiles meet without a crack.</summary>
    static List<(double X, double Y)> ClipSide(List<(double X, double Y)> poly, Func<(double X, double Y), double> inside,
        double at, bool vertical)
    {
        var o = new List<(double X, double Y)>(poly.Count + 4);
        if (poly.Count == 0)
            return o;
        var s = poly[^1];
        double ds = inside(s);
        foreach (var e in poly)
        {
            double de = inside(e);
            if ((de >= 0) != (ds >= 0))
            {
                double t = ds / (ds - de);
                o.Add(vertical ? (at, s.Y + (e.Y - s.Y) * t) : (s.X + (e.X - s.X) * t, at));
            }
            if (de >= 0)
                o.Add(e);
            (s, ds) = (e, de);
        }
        return o;
    }

    static double Area(List<(double X, double Y)> p)
    {
        double a = 0;
        for (int k = 0, j = p.Count - 1; k < p.Count; j = k++)
            a += p[j].X * p[k].Y - p[k].X * p[j].Y;
        return a / 2;
    }

    /// <summary>The coast as a line mesh: every ring, closed, two vertices a point.</summary>
    static (CoastVertex[], uint[]) CoastMesh(Land land)
    {
        var verts = new CoastVertex[2 * land.PointCount];
        var idx = new uint[6 * land.PointCount];
        int vi = 0, ii = 0;
        for (int r = 0; r < land.RingCount; r++)
        {
            var ring = land.Ring(r);
            int n = ring.Length;
            uint first = (uint)vi;
            for (int k = 0; k < n; k++)
            {
                var (prev, cur, next) = (F(ring[(k + n - 1) % n]), F(ring[k]), F(ring[(k + 1) % n]));
                verts[vi++] = new CoastVertex { Prev = prev, Cur = cur, Next = next, Side = 1 };
                verts[vi++] = new CoastVertex { Prev = prev, Cur = cur, Next = next, Side = -1 };
                uint a = first + (uint)(2 * k), b = first + (uint)(2 * ((k + 1) % n));
                idx[ii++] = a;
                idx[ii++] = a + 1;
                idx[ii++] = b;
                idx[ii++] = a + 1;
                idx[ii++] = b + 1;
                idx[ii++] = b;
            }
        }
        return (verts, idx);

        static Vector3 F(Vec3d p) => new((float)(p.X * Earth.Radius), (float)(p.Y * Earth.Radius), (float)(p.Z * Earth.Radius));
    }
}
