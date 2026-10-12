using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Combat;

/// <summary>Moves ships (docs/battle/plan.md step 2.2): the helmsman turns orders into a rudder angle, the rudder moves
/// at its gear's rate, the ship's rate of turn follows Nomoto's first-order model, its speed follows the forces of its
/// own power curve, and it moves along the arc its speed and rate of turn make. Every ship, every tick: nothing here
/// allocates, and a ship's step reads only its own slot, so ships can later be stepped in parallel.</summary>
public static class ShipMotion
{
    const double Deg = Math.PI / 180;

    /// <summary>Moves every live ship to <paramref name="nowMs"/> from wherever its pose is.</summary>
    public static void StepAll(ShipStore s, long nowMs, SimSettings set)
    {
        for (int i = 0; i < s.Slots; i++)
            if (s.alive[i] && !s.rails[i] && s.poseMs[i] < nowMs)
            {
                Step(s, i, (nowMs - s.poseMs[i]) / 1000.0, set);
                s.poseMs[i] = nowMs;
            }
    }

    /// <summary>One ship, dt seconds.</summary>
    public static void Step(ShipStore s, int i, double dt, SimSettings set)
    {
        var h = s.handling[i];
        double u0 = s.speed[i], r0 = s.yawRate[i];

        // the helm, then the rudder at its gear's rate
        double order = HelmOrder(s, i, h, set);
        double d = s.rudder[i], step = h.RudderRateRadS * dt;
        s.rudder[i] = d + Math.Clamp(order - d, -step, step);

        // the engines toward what's rung: power grows at the plant's rate, falls toward none at the throttle's
        double p = s.power[i], want = s.powerOrder[i];
        if (p != 0 && (Math.Sign(want) != Math.Sign(p) || Math.Abs(want) < Math.Abs(p)))
        {
            double cut = h.PowerCutWs * dt, to = Math.Sign(want) == Math.Sign(p) ? want : 0;
            p = p > to ? Math.Max(p - cut, to) : Math.Min(p + cut, to);
        }
        else
            p += Math.Clamp(want - p, -h.PowerRateWs * dt, h.PowerRateWs * dt);
        s.power[i] = p;

        // the rate of turn: T·dr/dt + r = K·δ, exactly over the step for the rudder now
        double target = h.SteadyRate(s.rudder[i], u0);
        double r1 = target + (r0 - target) * Math.Exp(-dt / h.T(u0, set));

        // speed: thrust from the power, resistance from the curve (more of it in a turn), implicit in the resistance
        // so a light fast boat is stable at any step
        double au = Math.Max(Math.Abs(u0), 0.01);
        double thrust = Math.Sign(p) * Math.Min(set.PropulsiveEfficiency * Math.Abs(p) / au, set.BollardThrustPerWatt * Math.Abs(p));
        double rs = au / h.SteadyRadiusM;   // the hard-over rate at this speed
        double q = Math.Min(r1 * r1 / (rs * rs), 1);
        double drag = set.PropulsiveEfficiency * h.PowerAtW(u0) * (1 + h.TurnDragFactor * q) / au;   // R(U), U ∝ R²
        double u1 = u0 + dt * (thrust - Math.Sign(u0) * drag) / (h.MassKg + dt * 2 * drag / au);
        if (thrust == 0 && Math.Sign(u1) != Math.Sign(u0))
            u1 = 0;   // resistance stops a ship, never backs it
        s.speed[i] = u1;
        s.yawRate[i] = r1;

        // along the arc of the step's mean speed and rate of turn
        var pos = s.position[i];
        var fwd = s.forward[i];
        Earth.Arc(ref pos, ref fwd, 0.5 * (u0 + u1) * dt, 0.5 * (r0 + r1) * dt);
        s.position[i] = pos;
        s.forward[i] = fwd;
    }

    /// <summary>Where the pose will be <paramref name="dt"/> seconds on if speed and rate of turn hold: what a renderer
    /// draws between ticks, by the same arc the step moves along.</summary>
    public static void Extrapolate(ref Vec3d position, ref Vec3d forward, double speed, double yawRate, double dt) =>
        Earth.Arc(ref position, ref forward, speed * dt, yawRate * dt);

    // ------------------------------------------------------------------ the helmsman

    /// <summary>The rudder the helmsman wants now, radians.</summary>
    static double HelmOrder(ShipStore s, int i, Handling h, SimSettings set)
    {
        switch (s.helm[i])
        {
            case Helm.Rudder:
                return s.rudderOrder[i];
            case Helm.Course:
                if (Math.Abs(s.position[i].Z) > Earth.Radius * Math.Sin(set.PolarCapDeg * Math.PI / 180))
                    s.courseDeg[i] = Earth.Heading(s.position[i], s.forward[i]);   // near a pole: steer straight on
                return Steer(s, i, h, s.courseDeg[i], set);
            case Helm.Route:
                return Steer(s, i, h, RouteCourse(s, i, h, set), set);
            default:
                return s.rudder[i];
        }
    }

    /// <summary>Steers for a course: rudder proportional to the heading error he expects a little ahead (the swing
    /// carried on for his lead time: the ship's answer to the helm, and the time to bring the rudder back), so he meets
    /// the swing before it overshoots; within the order's rudder.</summary>
    static double Steer(ShipStore s, int i, Handling h, double courseDeg, SimSettings set)
    {
        double heading = Earth.Heading(s.position[i], s.forward[i]);
        double e = courseDeg - heading;
        e -= 360 * Math.Round(e / 360);   // (-180, 180]: the shortest way round
        double lead = set.HelmLeadOverT * h.T(s.speed[i], set) + Math.Abs(s.rudder[i]) / h.RudderRateRadS;   // and the rudder's way back
        double ahead = e * Deg - s.yawRate[i] * lead;
        double limit = s.courseRudder[i];
        return Math.Clamp(set.HelmGain * ahead, -limit, limit) * Math.Sign(s.speed[i] >= 0 ? 1 : -1);
    }

    /// <summary>The course for the route's next waypoint, moving on to the one after at the wheel-over point (the
    /// turn's radius times the tangent of half the change of course, before it). Past the last, the helm holds the
    /// course it arrived on.</summary>
    static double RouteCourse(ShipStore s, int i, Handling h, SimSettings set)
    {
        int cap = s.RouteCapacity, n = s.routeCount[i];
        var pos = s.position[i];
        while (true)
        {
            int k = s.routeNext[i];
            var wp = s.route[i * cap + k];
            double course = Earth.Bearing(pos, wp);
            double dist = Earth.Distance(pos, wp);
            if (k + 1 < n)
            {
                var next = s.route[i * cap + k + 1];
                double arrive = Earth.Bearing(wp, next) - Earth.Bearing(wp, wp + (wp - pos));   // the change of course at wp
                arrive = Math.Abs(arrive - 360 * Math.Round(arrive / 360)) * Deg;
                double wheelOver = h.RadiusFor(s.courseRudder[i]) * Math.Tan(Math.Min(arrive, 170 * Deg) / 2);
                if (dist <= Math.Max(wheelOver, set.ArrivalRadiusM))
                {
                    s.routeNext[i] = k + 1;
                    continue;
                }
                return course;
            }
            // the last: reached inside the arrival radius, or once it's abeam or astern close by
            bool passed = Vec3d.Dot(s.forward[i], wp - pos) <= 0 && dist < 2 * h.RadiusFor(s.courseRudder[i]);
            if (dist <= set.ArrivalRadiusM || passed)
            {
                double hold = Earth.Heading(pos, s.forward[i]);
                s.routeCount[i] = s.routeNext[i] = 0;
                s.helm[i] = Helm.Course;
                s.courseDeg[i] = hold;
                return hold;
            }
            return course;
        }
    }
}
