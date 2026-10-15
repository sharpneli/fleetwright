using Fleetwright.Sim.Core;

namespace Fleetwright.Map.Wakes;

/// <summary>One point of a ship's trail: where its stern was (Earth-centred, m), when (game ms), its speed then (m/s),
/// and the distance it had steamed since the trail began (m: the foam's noise is fixed to the water by it).</summary>
public struct TrailPoint
{
    public Vec3d P;
    public long Ms;
    public float Speed;
    public double S;
}

/// <summary>A ship's trail (docs/visuals/wakes.md K4): its stern's path, sampled in game time every
/// <see cref="EveryMs"/> and whenever its heading has turned <see cref="TurnDeg"/> since the last point, so a hard turn
/// is a curve, not chords; kept <see cref="KeepMs"/>, at most <see cref="MaxPoints"/>. The table draws it as a line, the
/// world view as the foam lane (<see cref="FoamTrails"/>). It grows as it fills: thousands are made at once on a
/// scenario's first frame (2.7c: full-size ones were 11.5 MB and a collection).</summary>
public sealed class WakeTrail
{
    public const long EveryMs = 10_000, KeepMs = 600_000;   // 10 minutes (the user, 2026-10-13: 40 was too long)
    public const double TurnDeg = 3;
    public const int MaxPoints = 512;
    static readonly double TurnCos = Math.Cos(TurnDeg * Math.PI / 180);

    TrailPoint[] points = new TrailPoint[16];
    int head;   // where the next point goes
    Vec3d lastF;

    public int Count { get; private set; }
    public long Seen;

    /// <summary>The stern's offset along the ship (ship frame x, m), its waterline beam and its propulsor wash: set once
    /// the class is known (<see cref="Known"/>).</summary>
    public double SternX, Beam = 10, Wash = 1;
    public bool Known;

    /// <summary>The i-th point, oldest first.</summary>
    public ref readonly TrailPoint this[int i] => ref points[(head - Count + i + points.Length) % points.Length];

    public ref readonly TrailPoint Newest => ref this[Count - 1];

    /// <summary>Where the stern is now, given the ship's pose.</summary>
    public Vec3d Stern(Vec3d p, Vec3d f) => p + f * SternX;

    /// <summary>The head of the trail now: the stern at <paramref name="nowMs"/>, its speed, the distance on from the
    /// newest point.</summary>
    public TrailPoint Head(Vec3d p, Vec3d f, double speed, long nowMs)
    {
        var at = Stern(p, f);
        return new TrailPoint { P = at, Ms = nowMs, Speed = (float)speed, S = Count > 0 ? Newest.S + Earth.Distance(Newest.P, at) : 0 };
    }

    /// <summary>Samples the ship at <paramref name="nowMs"/>: a jump no ship could have steamed (snapped onto its stations
    /// on rails, or round to a new course) starts the trail again where it is, not drawn across the jump (a fan of lines
    /// to the new stations, the user's 2026-10-13); points older than <see cref="KeepMs"/> go.</summary>
    public void Sample(Vec3d p, Vec3d f, double speed, long nowMs)
    {
        var at = Stern(p, f);
        if (Count > 0)
        {
            ref readonly var last = ref Newest;
            double could = (Math.Abs(speed) + 3) * Math.Abs(nowMs - last.Ms) / 1000 + 100;
            if (Earth.Distance(at, last.P) > could)
                Count = 0;
        }
        while (Count > 0 && this[0].Ms < nowMs - KeepMs)
            Count--;
        if (Count > 0 && nowMs - Newest.Ms < EveryMs && Vec3d.Dot(f, lastF) > TurnCos)
            return;
        if (Count == points.Length)
        {
            if (points.Length < MaxPoints)
                Grow();
            else
                Count--;   // the oldest goes
        }
        double s = Count > 0 ? Newest.S + Earth.Distance(Newest.P, at) : 0;
        points[head] = new TrailPoint { P = at, Ms = nowMs, Speed = (float)speed, S = s };
        head = (head + 1) % points.Length;
        Count++;
        lastF = f;
    }

    /// <summary>Twice the room, the points kept in order from the oldest.</summary>
    void Grow()
    {
        var bigger = new TrailPoint[Math.Min(points.Length * 2, MaxPoints)];
        for (int i = 0; i < Count; i++)
            bigger[i] = this[i];
        points = bigger;
        head = Count;
    }
}
