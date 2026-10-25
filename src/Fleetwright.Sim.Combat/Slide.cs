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
/// <para>Coarse ships (docs/battle/plan.md step 2.8e) slide on the same feeler, turned at once onto its line for
/// legs (<see cref="CoarseCoast"/>), with a backstop of their own (<see cref="CoarseBackstop"/>).</para>
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

    // ------------------------------------------------------------------ coarse ships (docs/battle/plan.md step 2.8e)

    /// <summary>A coarse ship's segment, just laid (<see cref="CoarseMotion.Plan"/>), and the land: its end (ms), cut
    /// short of the coast. Sliding along it already, or with the segment coming within its standoff inside its
    /// look-ahead, it slides: the fine tick's own feeler asked at the anchor, and the ship turned at once onto the line
    /// it gives (the user, 2026-10-13: being this close to land is a mistake, turning in place is fine, as in Rule the
    /// Waves), for a leg as long as rails' (<see cref="SimSettings.SlideRailsStepM"/>, shorter where it would come near
    /// its minimum). Else the segment is cut where it would come within its standoff, less the look-ahead, to be laid
    /// again there; a segment longer than <see cref="SimSettings.RailsLandHorizonM"/> is looked at again there. Stopped
    /// or astern, only the backstop (<see cref="CoarseBackstop"/>). Out at sea, one lookup.</summary>
    public static long CoarseCoast(ShipStore s, int i, Shore shore, SimSettings set, long end)
    {
        var h = s.handling[i];
        var p = s.railPos[i];
        double v0 = s.railSpeed[i], top = end == long.MaxValue ? v0 : Math.Max(v0, s.SpeedAt(i, end));
        if (s.railGuide[i] >= 0 || v0 < 0 || top <= set.SlideMinSpeedMs)
            return end;
        double look = LookaheadM(h, top, s.courseRudder[i], set), standoff = h.StandoffM;
        if (s.landSide[i] != 0 && SlideLeg(s, i, shore, set, ref end))
            return end;

        // the segment traced for the coast, as far as it goes (or the horizon): a turn as two chords of its arc
        bool whole = end != long.MaxValue && s.Run(i, end) <= set.RailsLandHorizonM;
        double length = whole ? s.Run(i, end) : set.RailsLandHorizonM, c;
        if (shore.BoundM(p) > length + standoff)
            c = double.PositiveInfinity;
        else if (s.railCurv[i] != 0)
        {
            var f = s.railFwd[i];
            Vec3d mid = p, fm = f, last = p, fl = f;
            Earth.Circle(ref mid, ref fm, length / 2, s.railCurv[i]);
            Earth.Circle(ref last, ref fl, length, s.railCurv[i]);
            double half = Earth.Distance(p, mid);
            c = shore.FirstContact(p, mid - p, half, standoff);
            if (double.IsPositiveInfinity(c))
                c = half + shore.FirstContact(mid, last - mid, Earth.Distance(mid, last), standoff);
        }
        else if (!double.IsNaN(s.railCourse[i]))
            c = shore.FirstContactRhumb(p, s.railCourse[i], length, standoff);
        else
            c = shore.FirstContact(p, s.railFwd[i], length, standoff);
        if (c <= look && SlideLeg(s, i, shore, set, ref end))
            return end;
        double at = double.IsFinite(c) ? Math.Max(c - look, set.SlideRailsMinStepM) : whole ? double.PositiveInfinity : length - look;
        if (double.IsFinite(at))
            end = Math.Min(end, At(s, i, at));
        s.segEndMs[i] = end;
        return end;
    }

    /// <summary>A coarse ship near the coast: a route's waypoint near the slide passed as the fine tick passes it (inside
    /// the arrival radius, or abeam close by), then the feeler at its anchor. Sliding, it's turned onto the feeler's line
    /// at once, for a leg; let go, false (the segment laid from its orders stands).</summary>
    static bool SlideLeg(ShipStore s, int i, Shore shore, SimSettings set, ref long end)
    {
        var h = s.handling[i];
        var p = s.railPos[i];
        var f = s.railFwd[i];
        double radius = h.RadiusFor(s.courseRudder[i]);
        int cap = s.RouteCapacity;
        while (s.helm[i] == Helm.Route && s.routeNext[i] < s.routeCount[i])
        {
            var wp = s.route[i * cap + s.routeNext[i]];
            double dist = Earth.Distance(p, wp);
            if (dist > set.ArrivalRadiusM && (Vec3d.Dot(f, wp - p) > 0 || dist >= 2 * radius))
                break;
            int n = s.routeCount[i];
            if (s.routeNext[i] + 1 < n || s.routeLoop[i] && n > 1)
                s.routeNext[i] = (s.routeNext[i] + 1) % n;
            else
            {
                (s.helm[i], s.courseDeg[i]) = (Helm.Course, Earth.Heading(p, f));   // past the last: the course it came on
                s.routeCount[i] = s.routeNext[i] = 0;
            }
        }
        double want = s.helm[i] switch
        {
            Helm.Route when s.routeNext[i] < s.routeCount[i] => Earth.Bearing(p, s.route[i * cap + s.routeNext[i]]),
            Helm.Course => s.courseDeg[i],
            _ => Earth.Heading(p, f),
        };
        sbyte side = s.landSide[i];
        double heading = Feel(shore, h, p, f, Math.Max(s.railSpeed[i], set.SlideMinSpeedMs), s.courseRudder[i], want, ref side, set, out _);
        s.landSide[i] = side;
        if (side == 0)
            return false;
        var dir = Earth.Forward(p, heading);
        (s.railFwd[i], s.railCurv[i], s.railCourse[i]) = (dir, 0, double.NaN);   // turned at once onto the coast's line
        double near = shore.FirstContact(p, dir, set.SlideRailsStepM, h.MinClearanceM);
        double leg = Math.Min(set.SlideRailsStepM, Math.Max(0.5 * near, set.SlideRailsMinStepM));
        end = Math.Min(end, At(s, i, leg));
        s.segEndMs[i] = end;
        return true;
    }

    /// <summary>When a coarse ship's segment has run a distance, ms (at least a millisecond on); long.MaxValue for never.</summary>
    static long At(ShipStore s, int i, double metres)
    {
        double t = CoarseMotion.TimeToGo(s.railSpeed[i], s.railAccel[i], metres);
        return double.IsFinite(t) ? s.railMs[i] + WorldTime.AheadMs(t) : long.MaxValue;
    }

    /// <summary>Whether a coarse ship (anchored now) is on land or inside its minimum: one lookup at sea.</summary>
    public static bool CoarseInside(Shore shore, Handling h, Vec3d p) => shore.BoundM(p) < h.MinClearanceM && Inside(shore, p, h.MinClearanceM);

    /// <summary>The backstop for a coarse ship of its own, anchored now: inside its minimum (or on land), it's put back
    /// out on the minimum along the coast's normal and turned onto the coast's line, its speed kept only along it, as
    /// the fine tick's <see cref="Backstop"/>; and counted. True when it moved (to be laid again).</summary>
    public static bool CoarseBackstop(ShipStore s, int i, Shore shore, SimSettings set)
    {
        var h = s.handling[i];
        var p = s.railPos[i];
        if (s.railGuide[i] >= 0 || !CoarseInside(shore, h, p))
            return false;
        s.backstops[i]++;
        if (!ToSea(shore, ref p, h.MinClearanceM, set.CoastSearchM, out var n))
            return false;
        var up = p.Normalized();
        var f = s.railFwd[i];
        f = (f - up * Vec3d.Dot(f, up)).Normalized();
        if (Vec3d.Dot(f, n) <= 0)
        {
            var line = Vec3d.Cross(up, n);
            double along = Vec3d.Dot(f, line);
            s.railSpeed[i] *= Math.Abs(along);
            f = along >= 0 ? line : -line;
            if (s.helm[i] is Helm.Course or Helm.Route && s.landSide[i] == 0)
                s.landSide[i] = along >= 0 ? (sbyte)1 : (sbyte)-1;
        }
        (s.railPos[i], s.railFwd[i]) = (up * Earth.Radius, f);
        return true;
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
