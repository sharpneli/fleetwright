using Fleetwright.Sim.Core;

namespace Fleetwright.Map;

/// <summary>Brings a ring of the land inside a cap on the sphere without changing any winding number inside it. Points
/// inside stay; points outside are pulled in along their bearing from the centre onto the cap's edge; a run of points
/// outside becomes one walk along the edge by the net turn about the centre the run made; an edge crossing the cap's
/// edge is cut where it crosses. Nothing moves across a point inside the cap, so land or sea there is the original's
/// for rings of any size (Afro-Eurasia and the Americas span more than a hemisphere). A cap under 90° then puts the
/// whole ring in one hemisphere, where gnomonic projection and planar clipping are exact. Used per frame by
/// <see cref="LandView"/> (the view's cap) and once by <see cref="LandMesh"/> (each cube face's). Allocates nothing once
/// its buffer has grown.</summary>
public sealed class CapRetraction
{
    /// <summary>A point of a brought-in ring; Mask is 1 for a point on the cap's edge (pulled in, or walked), 0 for the
    /// ring's own points.</summary>
    public struct Pt
    {
        public Vec3d P;
        public int Mask;
    }

    const double WalkStep = 5 * Math.PI / 180;   // the longest step of a walk along the cap's edge

    Pt[] a = new Pt[4096];
    Vec3d centre;
    double capCos, capSin;

    // the ring being brought in: its length so far, its first point, the last point added; and the run outside the
    // cap under way, as the bearing it began at and the turn about the centre since (it can pass a full turn)
    int n;
    Vec3d first, prev, runFrom, runAt;
    double runTurn;

    /// <summary>The ring last brought in: <see cref="Retract"/>'s length of it. Callers may compact it in place.</summary>
    public Pt[] Points => a;

    public Vec3d Centre => centre;

    /// <summary>Sets the cap: its centre (a unit vector) and the cosine of its radius (under 1).</summary>
    public void Set(Vec3d capCentre, double cosRadius)
    {
        centre = capCentre;
        capCos = Math.Min(cosRadius, 1 - 1e-15);
        capSin = Math.Sqrt(1 - capCos * capCos);
    }

    public bool In(Vec3d p) => Vec3d.Dot(p, centre) >= capCos;

    /// <summary>Whether a cap (centre c, cos of its radius) is clear of this one: the angle between the centres is
    /// over the two radii, by cos(r + v) = cos r cos v - sin r sin v.</summary>
    public bool Clear(Vec3d c, double cos)
    {
        if (cos <= -capCos)
            return false;   // the radii add up to half a turn or more
        double sin = Math.Sqrt(Math.Max(0, 1 - cos * cos));
        return Vec3d.Dot(c, centre) < cos * capCos - sin * capSin;
    }

    /// <summary>Brings ring i of the land inside the cap, into <see cref="Points"/>; returns its length. A chunk whose cap
    /// is clear of this one comes in as its first point only: the loop between it and that shortcut stays inside its
    /// cap, outside this one, so it changes nothing inside, and a far coast costs a point per chunk. A point inside
    /// within the angle whose cosine is <paramref name="minStepCos"/> of the one before is dropped (pass 2 to keep all).</summary>
    public int Retract(Land land, int i, double minStepCos = 2)
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
            {
                var p = ring[k];
                if (k > 0 && Vec3d.Dot(p, prev) > minStepCos && In(p) && In(prev))
                    continue;
                Add(p, k == 0);
            }
        }
        Close();
        return n;
    }

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
        if (n + 2 > a.Length)
            Array.Resize(ref a, a.Length * 2);
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
}
