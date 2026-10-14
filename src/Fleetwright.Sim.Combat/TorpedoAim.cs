using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>A torpedo's aim (docs/battle/fighting.md 4.6): the course that meets a target going on at its estimated
/// course and speed, on the sea's plane at the launcher (runs are a few km), and which setting reaches it. Pure; the
/// torpedo system aims with it, and the captains ask it whether a target is within reach.</summary>
public static class TorpedoAim
{
    /// <summary>The course from a point (heading, degrees) that meets a target at a position going at a velocity (m/s),
    /// at a speed; the run to the meeting point, m. False when it can't catch it.</summary>
    public static bool Intercept(Vec3d from, Vec3d target, Vec3d velocity, double speedMs, out double headingDeg, out double runM)
    {
        var up = from.Normalized();
        var d = target - from;
        d -= up * Vec3d.Dot(d, up);
        var v = velocity - up * Vec3d.Dot(velocity, up);
        // |d + v·t| = u·t: (v·v - u²)t² + 2(d·v)t + d·d = 0, the first meeting
        double a = Vec3d.Dot(v, v) - speedMs * speedMs, b = 2 * Vec3d.Dot(d, v), c = Vec3d.Dot(d, d);
        double t;
        if (Math.Abs(a) < 1e-9)
            t = b < 0 ? -c / b : double.NaN;
        else
        {
            double disc = b * b - 4 * a * c;
            if (disc < 0)
                t = double.NaN;
            else
            {
                double r = Math.Sqrt(disc), t1 = (-b - r) / (2 * a), t2 = (-b + r) / (2 * a);
                t = Math.Min(t1, t2) > 0 ? Math.Min(t1, t2) : Math.Max(t1, t2);
            }
        }
        if (!(t > 0))
        {
            (headingDeg, runM) = (double.NaN, double.NaN);
            return false;
        }
        var aim = d + v * t;
        headingDeg = Earth.Heading(from, aim.Normalized());
        runM = speedMs * t;
        return true;
    }

    /// <summary>The setting that reaches a target (the fast one if it does, else the slow): its speed, m/s, and range,
    /// m, the course, and the run to the meeting point, m. False when neither does.</summary>
    public static bool Setting(in TorpedoRow t, Vec3d from, Vec3d target, Vec3d velocity, out double speedMs, out double rangeM, out double headingDeg,
        out double runM)
    {
        (speedMs, rangeM) = (t.FastKn * Handling.MsPerKnot, t.FastRangeM);
        if (Intercept(from, target, velocity, speedMs, out headingDeg, out runM) && runM <= rangeM)
            return true;
        (speedMs, rangeM) = (t.SlowKn * Handling.MsPerKnot, t.SlowRangeM);
        return Intercept(from, target, velocity, speedMs, out headingDeg, out runM) && runM <= rangeM;
    }
}
