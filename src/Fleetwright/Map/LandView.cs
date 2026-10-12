using System.Numerics;
using Fleetwright.Sim.Core;

namespace Fleetwright.Map;

/// <summary>The land as the map draws it, rebuilt on the CPU in double whenever the view changes: each ring near the
/// view is brought inside a cap around it, projected, and handed on as a fan of triangles for the fill (filled by
/// winding count on the GPU, so no triangulation) and as the coast's lines (<see cref="MapLines"/>).
/// <para>The cap encloses what's on screen: the screen's corners pushed 5% outward close in, the horizon when the limb
/// shows. A ring's points inside it stay; points outside are pulled in along their bearing from the centre onto its
/// edge, and between two such points the ring walks along the edge by the bearing the original arc swept. That moves
/// nothing across any point inside the cap, so every winding number there, land or sea, is the original's, for rings
/// of any size. Afro-Eurasia and the Americas span more than a hemisphere, which broke clipping by planes through the
/// centre: its joins along a plane's great circle could go the wrong way round. Walks along the edge are fill only,
/// never coast.</para>
/// <para>The level of detail follows the zoom: 1:110M for the globe, 1:50M for a theatre, 1:10M closer in. Allocates
/// nothing once its buffers have grown to the views it's shown.</para></summary>
public sealed class LandView(Land coarse, Land medium, Land fine)
{
    struct Pt
    {
        public Vec3d P;
        public int Mask;   // 1 for a point on the cap's edge (pulled in, or walked); 0 for the ring's own points
    }

    const double WalkStep = 5 * Math.PI / 180;   // the longest step of a walk along the cap's edge
    static readonly Vector4 CoastColor = new(0.86f, 0.80f, 0.60f, 0.55f);

    Pt[] a = new Pt[4096];
    Vector2[] fill = new Vector2[1 << 16];
    Vector2[] px = new Vector2[4096];
    int fillCount;

    // the cap: its centre (the view's), and the cosine and sine of its radius
    Vec3d centre;
    double capCos, capSin;

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
        SetCap(v);
        for (int i = 0; i < land.RingCount; i++)
        {
            var (c, cos) = land.Cap(i);
            if (Clear(c, cos))
                continue;
            int n = Retract(land, i);
            if (n >= 3)
                Emit(v, n, lines);
        }
        lines.End();
    }

    /// <summary>The cap for the view: through the farthest of the screen's corners pushed 5% outward, if all are on the
    /// globe, else the horizon (a hair inside it, so every point projects in front).</summary>
    void SetCap(in MapView v)
    {
        const double M = 0.05;
        centre = -v.Forward;
        double horizon = Earth.Radius / (Earth.Radius + v.Altitude);
        capCos = horizon + 1e-9 * (1 - horizon);
        if (v.Pick(-M * v.Width, -M * v.Height, out var c0) && v.Pick((1 + M) * v.Width, -M * v.Height, out var c1)
            && v.Pick((1 + M) * v.Width, (1 + M) * v.Height, out var c2) && v.Pick(-M * v.Width, (1 + M) * v.Height, out var c3))
            capCos = Math.Max(capCos, Math.Min(Math.Min(Cos(c0), Cos(c1)), Math.Min(Cos(c2), Cos(c3))));
        capCos = Math.Min(capCos, 1 - 1e-15);
        capSin = Math.Sqrt(1 - capCos * capCos);
    }

    double Cos(Vec3d p) => Vec3d.Dot(p.Normalized(), centre);

    /// <summary>Whether a cap (centre c, cos of its radius) is clear of the view's: the angle between the centres is
    /// over the two radii, by cos(r + v) = cos r cos v - sin r sin v.</summary>
    bool Clear(Vec3d c, double cos)
    {
        if (cos <= -capCos)
            return false;   // the radii add up to half a turn or more
        double sin = Math.Sqrt(Math.Max(0, 1 - cos * cos));
        return Vec3d.Dot(c, centre) < cos * capCos - sin * capSin;
    }

    /// <summary>Brings ring i inside the cap (the result in a, its length returned). A chunk whose cap is clear of the
    /// view's comes in as its first point only: the loop between it and that shortcut stays inside its cap, outside
    /// the view's, so it changes nothing in view, and a far coast costs a point per chunk.</summary>
    int Retract(Land land, int i)
    {
        var ring = land.Ring(i);
        n = 0;
        first = ring[0];
        for (int j = 0; j < land.ChunkCount(i); j++)
        {
            int from = j * Land.ChunkSize, to = Math.Min(from + Land.ChunkSize, ring.Length);
            var (cc, cos) = land.ChunkCap(i, j);
            if (cos > 0 && Clear(cc, cos))
                to = from + 1;
            for (int k = from; k < to; k++)
                Add(ring[k], k == 0);
        }
        Close();
        return n;
    }

    // the ring being brought in: its length so far, its first point, the last point added; and the run outside the
    // cap under way, as the bearing it began at and the turn about the centre since (it can pass a full turn)
    int n;
    Vec3d first, prev, runFrom, runAt;
    double runTurn;

    bool In(Vec3d p) => Vec3d.Dot(p, centre) >= capCos;

    /// <summary>Adds a point of the ring. Inside the cap it stays. A run of points outside becomes one walk along the
    /// cap's edge, from where the ring left to where it comes back, by the net turn about the centre the run made:
    /// all that the windings inside depend on, so a far coast that only wanders costs nothing. An edge crossing the
    /// cap's edge is cut exactly where it crosses, so its part inside is the original's.</summary>
    void Add(Vec3d p, bool isFirst)
    {
        if (n + 2 > a.Length)
            Array.Resize(ref a, a.Length * 2);
        bool inside = In(p);
        if (isFirst)
        {
            if (inside)
                a[n++] = new Pt { P = p };
            else
                StartRun(Bearing(p), emit: true);   // the ring starts outside: its first point, on the cap's edge
        }
        else
        {
            bool prevIn = In(prev);
            if (prevIn && !inside)
            {
                var x = Crossing(prev, p);   // leaving: the crossing, and a run begins there
                a[n++] = new Pt { P = x, Mask = 1 };
                StartRun(Bearing(x), emit: false);
                Turn(Bearing(p));
            }
            else if (!prevIn && inside)
            {
                var x = Crossing(p, prev);   // coming back: the run's walk to the crossing, then the point
                Turn(Bearing(x));
                EndRun();
                a[n++] = new Pt { P = x, Mask = 1 };
                a[n++] = new Pt { P = p };
            }
            else if (inside)
                a[n++] = new Pt { P = p };
            else
                Turn(Bearing(p));
        }
        prev = p;
    }

    void StartRun(Vec3d dir, bool emit)
    {
        (runFrom, runAt, runTurn) = (dir, dir, 0);
        if (emit)
            a[n++] = new Pt { P = centre * capCos + dir * capSin, Mask = 1 };
    }

    /// <summary>The run moves on to a bearing: the turn from the last one, under half a turn as the arc between them
    /// stays outside the cap.</summary>
    void Turn(Vec3d dir)
    {
        runTurn += Math.Atan2(Vec3d.Dot(centre, Vec3d.Cross(runAt, dir)), Vec3d.Dot(runAt, dir));
        runAt = dir;
    }

    /// <summary>The run's walk: points along the cap's edge from where it began, by its net turn, strictly between
    /// its ends (the caller adds the end).</summary>
    void EndRun()
    {
        int steps = (int)Math.Ceiling(Math.Abs(runTurn) / WalkStep);
        if (steps < 2)
            return;
        if (n + steps + 2 > a.Length)
            Array.Resize(ref a, Math.Max(a.Length * 2, n + steps + 2));
        var side = Vec3d.Cross(centre, runFrom);
        for (int s = 1; s < steps; s++)
        {
            double t = runTurn * s / steps;
            a[n++] = new Pt { P = centre * capCos + (runFrom * Math.Cos(t) + side * Math.Sin(t)) * capSin, Mask = 1 };
        }
    }

    /// <summary>The edge from the last point back to the first.</summary>
    void Close()
    {
        if (n == 0)
            return;
        bool lastIn = In(prev), firstIn = In(first);
        if (lastIn && !firstIn)
        {
            var x = Crossing(prev, first);   // leaving for the first point, which is on the cap's edge already
            a[n++] = new Pt { P = x, Mask = 1 };
            StartRun(Bearing(x), emit: false);
            Turn(Bearing(first));
            EndRun();
        }
        else if (!lastIn && firstIn)
        {
            var x = Crossing(first, prev);
            Turn(Bearing(x));
            EndRun();
            a[n++] = new Pt { P = x, Mask = 1 };
        }
        else if (!lastIn)
        {
            Turn(Bearing(first));   // the run that began this ring's first point: it ends there
            EndRun();
        }
    }

    /// <summary>Where the arc from <paramref name="inside"/> to <paramref name="outside"/> crosses the cap's edge.</summary>
    Vec3d Crossing(Vec3d inside, Vec3d outside)
    {
        double lo = 0, hi = 1;
        for (int k = 0; k < 50; k++)
        {
            double t = (lo + hi) / 2;
            if (In((inside + (outside - inside) * t).Normalized()))
                lo = t;
            else
                hi = t;
        }
        var q = (inside + (outside - inside) * lo).Normalized();
        return centre * capCos + Bearing(q) * capSin;   // exactly on the edge
    }

    /// <summary>The bearing of a point from the centre: the unit tangent at the centre toward it.</summary>
    Vec3d Bearing(Vec3d p)
    {
        var t = p - centre * Vec3d.Dot(p, centre);
        double l = t.Length;
        return l > 1e-15 ? t / l : Earth.East(centre);   // the antipode: any bearing will do
    }

    /// <summary>Projects the ring, fans it into the fill and draws its coast (not its walks along the cap's edge).</summary>
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
        // the coast: every edge but those along the cap's edge
        lines.BeginStrip(CoastColor, 1.2f);
        int start = 0;
        for (int k = 0; k < n; k++)
            if ((a[k].Mask & a[(k + 1) % n].Mask) != 0)
            {
                start = (k + 1) % n;   // begin after a walk, so a strip never wraps across one
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
