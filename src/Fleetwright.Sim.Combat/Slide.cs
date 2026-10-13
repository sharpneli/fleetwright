using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Combat;

/// <summary>Ships and the land in the fine tier (docs/world/terrain-plan.md, "The slide"): Rule the Waves' slide along
/// the coast, done with the ship's own helm. Two parts, both reading only the ship's slot and the immutable
/// <see cref="Shore"/>, so ships can be stepped in parallel; nothing allocates.
/// <list type="bullet">
/// <item><b>The feeler</b> (<see cref="Course"/>): between the course the helmsman wants and the one he steers. Where
/// the wanted course would bring the ship inside its standoff within its look-ahead, he steers along the coast's line
/// instead, on the side nearer the wanted course, pushed toward the standoff, until the wanted course is clear again.
/// The order is never changed: the player and captains compare against what they ordered.</item>
/// <item><b>The backstop</b> (<see cref="Backstop"/>): after the step, a ship inside its minimum is put back on it and
/// turned along the coast, Rule the Waves' snap, as the failure path under the feeler. Counted.</item>
/// </list>
/// <para>Every question about land goes through this file, so the guided areas to come (gates closing a strait,
/// canals; the plan's "Guided areas") add their own lines here and nowhere else.</para></summary>
public static class Slide
{
    const double Deg = Math.PI / 180;

    /// <summary>How far ahead the feeler looks, metres: the radius the ship turns at with its course rudder (a turn
    /// onto the coast's line fits in it) and <see cref="SimSettings.SlideLookaheadS"/> of its speed.</summary>
    public static double LookaheadM(Handling h, double speedMs, double courseRudderRad, SimSettings set) =>
        h.RadiusFor(courseRudderRad) + Math.Abs(speedMs) * set.SlideLookaheadS;

    /// <summary>The course the helmsman steers for the one he wants, degrees: <see cref="Feel"/> at the ship's stepped
    /// pose, keeping its side in the store. Stopped or astern, only the backstop.</summary>
    public static double Course(ShipStore s, int i, Handling h, double wantDeg, Shore? shore, SimSettings set, out bool hardOver)
    {
        hardOver = false;
        if (shore == null || s.speed[i] < set.SlideMinSpeedMs)
            return wantDeg;
        return Feel(shore, h, s.position[i], s.forward[i], s.speed[i], s.courseRudder[i], wantDeg, ref s.landSide[i], set, out hardOver);
    }

    /// <summary>The feeler, for a pose (the fine tier's stepped one, or where rails have a ship, S3): the course to
    /// steer for the wanted one, degrees: the wanted one, or near land the coast's line at the standoff.
    /// <paramref name="side"/> is which way along the coast it slides (land to port +1, to starboard -1, 0 not sliding),
    /// chosen when it starts and kept until it's let go. <paramref name="hardOver"/>: the coast ahead closes in tighter
    /// than the ordinary rudder turns (a corner nearer than <see cref="SimSettings.SlideHardOverRadii"/> of its radius,
    /// or the coast nearer than <see cref="SimSettings.SlideHardOverStandoffs"/>), so the helmsman may put it hard over.</summary>
    public static double Feel(Shore shore, Handling h, Vec3d p, Vec3d forward, double speed, double courseRudder, double wantDeg,
        ref sbyte side, SimSettings set, out bool hardOver)
    {
        hardOver = false;
        double look = LookaheadM(h, speed, courseRudder, set), standoff = h.StandoffM;
        if (shore.BoundM(p) > look * set.SlideReleaseFactor + standoff)
        {
            side = 0;   // nothing near: one lookup, nearly every ship, nearly every tick
            return wantDeg;
        }

        // is the wanted course clear? Within the standoff, only coming closer than the ship already is counts
        double d = shore.Clearance(p, look * set.SlideReleaseFactor + standoff, out var near);
        bool onLand = shore.IsLand(p);
        double here = onLand ? -d : d;
        double clear = Math.Max(Math.Min(standoff, here - set.SlideToleranceM), h.MinClearanceM);
        // sliding, it's let go only once the wanted course is clear further on and near its heading: a long turn back
        // to it would sweep through the coast it's beside
        var want = Earth.Forward(p, wantDeg);
        bool sliding = side != 0;
        double reach = sliding ? look * set.SlideReleaseFactor : look, off0 = wantDeg - Earth.Heading(p, forward);
        bool turned = Math.Abs(off0 - 360 * Math.Round(off0 / 360)) <= set.SlideReleaseDeg;
        if ((!sliding || turned) && double.IsPositiveInfinity(shore.FirstContact(p, want, reach, clear)))
        {
            side = 0;
            return wantDeg;
        }

        // sliding: along the coast where the ship's own heading would first come inside its standoff, so a corner is
        // turned for before the ship is in it; else along the coast nearest a point a turning radius ahead; failing
        // that (heading away from it), the coast nearest the ship
        var up = p.Normalized();
        double radius = h.RadiusFor(courseRudder);
        double corner = shore.FirstContact(p, forward, look, clear);
        var q = p;
        var fq = forward;
        Earth.Advance(ref q, ref fq, double.IsPositiveInfinity(corner) ? radius : corner);
        double dq = shore.Clearance(q, look * set.SlideReleaseFactor + standoff, out var nq);
        Vec3d at = q, coast = nq;
        bool atLand = false;
        if (double.IsPositiveInfinity(dq))
        {
            if (double.IsPositiveInfinity(d))
                return wantDeg;
            (at, coast, dq, atLand) = (p, near, d, onLand);
        }
        else
            atLand = shore.IsLand(q);
        if (!Seaward(at, coast, atLand, up, out var n))
            return wantDeg;

        // along the line, land to port (+1: up × n, whose starboard is n) or to starboard (-1); kept once chosen
        var line = Vec3d.Cross(up, n);
        if (side == 0)
        {
            // the way nearer the wanted course; near square to the coast, to starboard of it (of the order, not the
            // heading, so the two tiers, and replays from either, choose alike)
            double w = Vec3d.Dot(line, want);
            if (Math.Abs(w) < Math.Sin(set.SlideSquareDeg * Deg))
                w = Vec3d.Dot(line, Vec3d.Cross(want, up));
            side = w >= 0 ? (sbyte)1 : (sbyte)-1;
        }
        double off = atLand ? -dq : dq;
        hardOver = corner < set.SlideHardOverRadii * radius || off < set.SlideHardOverStandoffs * standoff;
        double turn = set.SlideCorrectionDeg * Deg * Math.Clamp((standoff - off) / standoff, -1, 1);
        var dir = line * (side * Math.Cos(turn)) + n * Math.Sin(turn);
        return Earth.Heading(p, dir);
    }

    /// <summary>The backstop, after a ship's step: inside its minimum (or on land), it's put back out on the minimum
    /// along the coast's normal, turned onto the coast's line (the way nearer its heading, unless it already heads
    /// out), its speed kept only along it, its swing stopped; and counted. A cell lookup at sea.</summary>
    public static void Backstop(ShipStore s, int i, Handling h, Shore shore, SimSettings set)
    {
        var p = s.position[i];
        if (shore.BoundM(p) >= h.MinClearanceM || !Inside(shore, p, h.MinClearanceM))
            return;
        s.backstops[i]++;
        if (!ToSea(shore, ref p, h.MinClearanceM, set.CoastSearchM, out var n))
            return;   // nowhere near the sea (only a ship put deep inland, by rails until terrain plan S2)
        s.position[i] = p;
        var up = p.Normalized();
        var f = s.forward[i];
        f = (f - up * Vec3d.Dot(f, up)).Normalized();
        if (Vec3d.Dot(f, n) <= 0)
        {
            var line = Vec3d.Cross(up, n);
            double along = Vec3d.Dot(f, line);
            var g = along >= 0 ? line : -line;
            s.speed[i] *= Math.Abs(along);
            f = g;
            if (s.helm[i] is Helm.Course or Helm.Route && s.landSide[i] == 0)
                s.landSide[i] = along >= 0 ? (sbyte)1 : (sbyte)-1;   // the feeler slides on the way it was put
        }
        s.forward[i] = f;
        s.yawRate[i] = 0;
    }

    /// <summary>Moves a point (at the Earth's radius) to sea at least <paramref name="clearM"/> off the coast if it's on
    /// land or nearer than that: out along the nearest coast's normal, a few times over for corners. False when it
    /// can't (no coast within <paramref name="searchM"/> of a point on land, or water too narrow for the clearance).
    /// <paramref name="seaward"/> is the last push's direction at the point, zero if it didn't move.</summary>
    public static bool ToSea(Shore shore, ref Vec3d p, double clearM, double searchM, out Vec3d seaward)
    {
        seaward = default;
        for (int k = 0; k < 4; k++)
        {
            var up = p.Normalized();
            bool land = shore.IsLand(up);
            double d = shore.Clearance(up, land ? searchM : clearM, out var near);
            if (!land && double.IsPositiveInfinity(d))
                return true;
            if (double.IsPositiveInfinity(d) || !Seaward(up, near, land, near, out var n))
                return false;
            var q = near * Earth.Radius;
            var f = n;
            Earth.Advance(ref q, ref f, clearM * 1.01 + 1);
            p = q;
            seaward = f;
        }
        return !Inside(shore, p, clearM);
    }

    /// <summary>Whether a point is on land or within a distance of the coast.</summary>
    static bool Inside(Shore shore, Vec3d p, double m) =>
        shore.IsLand(p) || !double.IsPositiveInfinity(shore.Clearance(p, m, out _));

    /// <summary>The unit tangent at <paramref name="tangentAt"/> pointing from the coast to the sea, for a point and
    /// its nearest coast point; false when they coincide.</summary>
    static bool Seaward(Vec3d point, Vec3d coast, bool pointOnLand, Vec3d tangentAt, out Vec3d n)
    {
        var u = tangentAt.Normalized();
        var d = point.Normalized() - coast;
        if (pointOnLand)
            d = -d;
        d -= u * Vec3d.Dot(d, u);
        double l = d.Length;
        n = l > 1e-12 ? d / l : default;
        return l > 1e-12;
    }
}
