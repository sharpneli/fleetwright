using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai.Captains;

/// <summary>The track a followed ship steamed, as its follower's captain saw it (docs/battle/plan.md step 2.6): its
/// positions a step apart, newest last, in a ring that drops the oldest. A column follows it, so each ship turns where
/// the one ahead turned (turns in succession) rather than cutting across to a station on the leader's new heading.
/// Made once per captain who follows a track, and reused.</summary>
public sealed class Trail(int capacity)
{
    readonly Vec3d[] points = new Vec3d[Math.Max(capacity, 4)];
    int head, count;

    public int Count => count;

    /// <summary>The ship the trail is of, and when it was last recorded (ms of world time): a trail not recorded for a
    /// while (its follower was on rails) is laid again.</summary>
    public int Of { get; private set; }
    public long LastMs { get; private set; }

    /// <summary>The k-th newest point (0 the newest).</summary>
    public Vec3d Newest(int k) => points[((head - 1 - k) % points.Length + points.Length) % points.Length];

    /// <summary>A trail laid straight astern of a ship along its heading, a step apart, as far as given: what a ship
    /// steaming steady left behind (a column on rails, or one just formed).</summary>
    public void Lay(int of, Vec3d position, Vec3d forward, double lengthM, double stepM, long nowMs)
    {
        (head, count, Of, LastMs) = (0, 0, of, nowMs);
        int n = Math.Min(points.Length, (int)Math.Ceiling(lengthM / stepM));
        for (int k = n; k >= 1; k--)
            Push((position - forward * (k * stepM)).Normalized() * Earth.Radius);
    }

    /// <summary>Adds the ship's position if it has come a step from the newest point.</summary>
    public void Record(Vec3d position, double stepM, long nowMs)
    {
        LastMs = nowMs;
        if (count == 0 || Earth.Distance(position, Newest(0)) >= stepM)
            Push(position);
    }

    void Push(Vec3d p)
    {
        points[head] = p;
        head = (head + 1) % points.Length;
        count = Math.Min(count + 1, points.Length);
    }

    public void Hash(ref StateHash h)
    {
        h.Add(Of);
        h.Add(LastMs);
        h.Add(count);
        for (int k = 0; k < count; k++)
        {
            var p = Newest(k);
            h.Add(p.X);
            h.Add(p.Y);
            h.Add(p.Z);
        }
    }
}
