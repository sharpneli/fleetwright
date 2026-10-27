using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Combat;

/// <summary>Coarse ships (docs/battle/plan.md step 2.8): kinematic motion with nothing to settle, as in Rule the Waves. A
/// coarse ship's speed goes toward what its engines' power holds at its class's constant acceleration
/// (<see cref="Handling.CoarseAccelUp"/> and the others, the fine model's own over its trials), and it turns at once on
/// the circle of its course rudder (<see cref="Handling.RadiusFor"/>), with no rudder to move and no swing to build. So
/// its track is segments in closed form: an arc of constant curvature or a straight leg (a rhumb line on a course, a
/// great circle for a waypoint), at a constant acceleration, each ending when its turn or change of speed is done or
/// at a waypoint's wheel-over. The store evaluates a segment at any time (<see cref="ShipStore.PoseAt"/>), and its
/// end is an event that lays the next: the coarse step decides, it doesn't move anything, so how long it is doesn't
/// change where ships go. Laying a segment again from the same pose and orders lays the same one.
/// <para>The helm is the fine helmsman's: a course is steered to and held, a route's waypoints are steered for and
/// passed at his wheel-over point (the turn's radius times the tangent of half the change of course, at most the
/// radius), the last held on the course it arrived on. A rudder held turns for ever. Nothing here allocates.</para></summary>
public static class CoarseMotion
{
    const double Deg = Math.PI / 180;

    /// <summary>Speeds nearer than this, m/s, are the same (what a segment's end leaves of a change of speed).</summary>
    const double SpeedEpsilon = 1e-3;

    /// <summary>Lays a coarse ship's segment from its anchor (just made: <see cref="ShipStore.Coarsen"/> or
    /// <see cref="ShipStore.ReanchorCoarse"/>) and its orders; when it ends, ms, or long.MaxValue for never.</summary>
    public static long Plan(ShipStore s, int i, SimSettings set)
    {
        var h = s.handling[i];
        var p = s.railPos[i];
        var f = s.railFwd[i];
        double v0 = s.railSpeed[i];

        // the speed: toward what the power rung holds, through a stop in two segments (so a segment's way is one way)
        double po = s.powerOrder[i];
        double v1 = po >= 0 ? h.SteadySpeedMs(po) : -h.SteadySpeedMs(-po);
        if (v0 > 0 && v1 < 0 || v0 < 0 && v1 > 0)
            v1 = 0;
        double a = 0, tSpeed = double.PositiveInfinity;
        if (Math.Abs(v1 - v0) > SpeedEpsilon)
        {
            double mag = v1 > v0 && v0 >= 0 ? h.CoarseAccelUp : po < 0 || v0 < 0 ? h.CoarseAccelAstern : h.CoarseAccelDown;
            a = Math.Sign(v1 - v0) * mag;
            tSpeed = (v1 - v0) / a;
        }
        else
            s.railSpeed[i] = v0 = v1;
        int dir = Math.Sign(v0 != 0 ? v0 : v1);   // ahead, astern, or not moving

        // the helm: the turn's curvature and how far it goes (or a straight leg's length to its end), the leg's line
        double curv = 0, dist = double.PositiveInfinity, course = double.NaN;
        bool free = s.steering[i] is Steering.Free or Steering.Hand or Steering.Engines;   // damage: a jammed rudder turns for ever, one lost trails amidships (by hand or engines she steers, roughly)
        switch (free ? s.helm[i] : Helm.None)
        {
            case Helm.Rudder:
            case Helm.None:
                s.leadEndMs[i] = 0;
                double rudder = !free ? s.jamRudder[i] : s.helm[i] == Helm.Rudder ? s.rudderOrder[i] : s.rudder[i];
                if (rudder != 0)
                    curv = Math.Sign(rudder) / h.RadiusFor(rudder);   // going astern the same circle runs backward
                break;
            case Helm.Route when Route(s, i, h, set, p, ref f, dir, v0, a, out curv, out dist):
                break;
            default:
                if (dir == 0)
                    break;   // not moving: nothing to turn
                if (Earth.InPolarCap(p, set.PolarCapDeg))
                {
                    s.courseDeg[i] = Earth.Heading(p, f);   // near a pole: steer straight on, on the great circle
                    break;
                }
                double err = Wrap(s.courseDeg[i] - Earth.Heading(p, f));
                if (Math.Abs(err) <= set.CoarseSnapDeg)
                {
                    s.leadEndMs[i] = 0;
                    course = s.courseDeg[i];   // on it: a rhumb line at the course exactly
                    f = Earth.Forward(p, course);
                    break;
                }
                if (Lead(s, i, h, set, v0, a, Math.Sign(err), out dist))
                {
                    course = Earth.Heading(p, f);   // the swing building: straight on the heading she has
                    f = Earth.Forward(p, course);
                    break;
                }
                if (s.leadEndMs[i] != 0 && Math.Abs(err) > 179)
                    err = s.leadSide[i] * Math.Abs(err);   // near right astern: the side chosen as the lead began
                double radius = h.RadiusFor(s.courseRudder[i]);
                curv = Math.Sign(err) * dir / radius;
                dist = Math.Abs(err) * Deg * radius;
                break;
        }

        s.railFwd[i] = f;
        s.railCourse[i] = curv == 0 ? course : double.NaN;
        s.railCurv[i] = curv;
        s.railAccel[i] = a;
        double t = Math.Min(tSpeed, TimeToGo(v0 * dir, a * dir, dist));
        long end = double.IsFinite(t) ? s.railMs[i] + WorldTime.AheadMs(t) : long.MaxValue;
        s.segEndMs[i] = end;
        return end;
    }

    /// <summary>A route's next segment: on to the next waypoint once inside the wheel-over point (or, past the last,
    /// the course it arrived on held: false), then the turn onto the line for it, or the straight leg to the next
    /// wheel-over point (or to the last waypoint).</summary>
    static bool Route(ShipStore s, int i, Handling h, SimSettings set, Vec3d p, ref Vec3d f, int dir, double v0, double a, out double curv, out double dist)
    {
        (curv, dist) = (0, double.PositiveInfinity);
        int cap = s.RouteCapacity, n = s.routeCount[i];
        bool loop = s.routeLoop[i] && n > 1;
        double radius = h.RadiusFor(s.courseRudder[i]);
        for (int tries = 0; ; tries++)
        {
            int k = s.routeNext[i];
            if (k >= n)
                return Arrived(s, i, p, f);
            var wp = s.route[i * cap + k];
            double d = Earth.Distance(p, wp), wheelOver = 0;
            bool more = k + 1 < n || loop;
            if (more && tries <= n)   // more tries than legs: a loop smaller than the turn, steered for as it comes
            {
                var next = s.route[i * cap + (k + 1) % n];
                double change = Earth.Bearing(wp, next) - Earth.Bearing(wp, wp + (wp - p));
                change = Math.Abs(change - 360 * Math.Round(change / 360)) * Deg;
                wheelOver = radius * Math.Tan(Math.Min(change, 90 * Deg) / 2);
                if (d <= Math.Max(wheelOver, set.ArrivalRadiusM))
                {
                    s.routeNext[i] = (k + 1) % n;
                    continue;
                }
            }
            else if (!more && (d <= set.ArrivalRadiusM || Vec3d.Dot(f, wp - p) <= 0 && d < 2 * radius))
                return Arrived(s, i, p, f);   // the last: reached inside the arrival radius, or abeam or astern close by
            if (dir <= 0)
                return true;   // astern or not moving: nothing steered for a waypoint, straight on
            double end = d - (more ? Math.Max(wheelOver, set.ArrivalRadiusM) : set.ArrivalRadiusM);   // where it's passed, as above
            double rel = Wrap(Earth.Bearing(p, wp) - Earth.Heading(p, f)) * Deg;
            if (Math.Abs(rel) <= set.CoarseSnapDeg * Deg)
            {
                s.leadEndMs[i] = 0;
                f = Toward(p, wp);   // on the line: the great circle for it, to where it's passed
                dist = end;
                return true;
            }
            if (Lead(s, i, h, set, v0, a, Math.Sign(rel), out dist))
                return true;   // the swing building: straight on along her heading (the great circle)
            // the turn whose tangent runs through the waypoint, in the tangent plane at p (ahead x, to starboard y),
            // mirrored to starboard: on the circle round (0, R), heading ψ is at (R sin ψ, R (1 - cos ψ)), and the
            // waypoint lies on its tangent where (wy - R) cos ψ - wx sin ψ = -R
            double side = Math.Sign(rel), wx = d * Math.Cos(rel), wy = d * Math.Abs(Math.Sin(rel));
            double A = wy - radius, B = -wx, m = Math.Sqrt(A * A + B * B);
            if (m <= radius)
            {
                dist = radius;   // inside the circle it turns on: on ahead until it can turn for it
                return true;
            }
            double alpha = Math.Atan2(B, A), beta = Math.Acos(-radius / m);
            double psi = Math.Min(Tangent(alpha + beta, wx, wy, radius), Tangent(alpha - beta, wx, wy, radius));
            if (!double.IsFinite(psi))
                return true;
            curv = side / radius;
            dist = psi * radius;
            return true;
        }
    }

    /// <summary>A turn about to be laid from straight (docs/battle/coarse.md J4): the fine tick's heading runs
    /// <see cref="Handling.HelmLagS"/> behind a turn started at once (the rudder going over, the swing building), so a
    /// coarse ship not swinging when anchored runs straight on for that long first, then turns on her circle; one
    /// swinging already turns on. Laid again within it (an order, a change of speed's end), the lead runs to the same
    /// end. True with the lead's distance to go.</summary>
    static bool Lead(ShipStore s, int i, Handling h, SimSettings set, double v0, double a, int side, out double dist)
    {
        dist = double.PositiveInfinity;
        long now = s.railMs[i];
        if (s.leadEndMs[i] == 0)
        {
            if (s.anchorCurv[i] != 0)
                return false;
            s.leadEndMs[i] = now + WorldTime.AheadMs(h.HelmLagS(Math.Abs(v0), s.courseRudder[i], set));
            s.leadSide[i] = (sbyte)side;
        }
        if (now >= s.leadEndMs[i])
            return false;   // the swing's built: the turn (and the lead stays spent until she's on her course)
        double t = (s.leadEndMs[i] - now) / 1000.0;
        dist = Math.Abs(v0 * t + 0.5 * a * t * t);
        return dist > 0;
    }

    /// <summary>A solution's turn in [0, 2π), if heading along the tangent there goes toward the waypoint (the other
    /// root's heads away from it); else +inf.</summary>
    static double Tangent(double psi, double wx, double wy, double radius)
    {
        psi -= 2 * Math.PI * Math.Floor(psi / (2 * Math.PI));
        return wx * Math.Cos(psi) + (wy - radius) * Math.Sin(psi) > 0 ? psi : double.PositiveInfinity;
    }

    /// <summary>Past the route's last waypoint: the helm holds the course it arrived on.</summary>
    static bool Arrived(ShipStore s, int i, Vec3d p, Vec3d f)
    {
        s.routeCount[i] = s.routeNext[i] = 0;
        s.helm[i] = Helm.Course;
        s.courseDeg[i] = Earth.Heading(p, f);
        return false;
    }

    /// <summary>The time to go a distance from a speed at a constant acceleration (both along the way the ship goes),
    /// s; +inf if it never does (it stops short, or isn't moving).</summary>
    internal static double TimeToGo(double u, double a, double distance)
    {
        if (!double.IsFinite(distance))
            return double.PositiveInfinity;
        if (a == 0)
            return u > 0 ? distance / u : double.PositiveInfinity;
        double disc = u * u + 2 * a * distance;
        if (disc < 0)
            return double.PositiveInfinity;
        double den = u + Math.Sqrt(disc);
        return den > 0 ? 2 * distance / den : double.PositiveInfinity;
    }

    static double Wrap(double deg) => deg - 360 * Math.Round(deg / 360);

    static Vec3d Toward(Vec3d from, Vec3d to)
    {
        var up = from.Normalized();
        var d = to - from;
        return (d - up * Vec3d.Dot(d, up)).Normalized();
    }
}
