using Fleetwright.Sim.Ai.Knowledge;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai.Captains;

/// <summary>What each captain's order wants, as a course and a speed: pure functions of his own ship and what he knows
/// of the other, so they're tested on their own and the same on every run.</summary>
public static class Behaviours
{
    /// <summary>Follow: keep station <paramref name="distanceM"/> astern of the leader (or
    /// <see cref="SimSettings.FollowDistanceLengths"/> of his own lengths when 0). He steers for a point on the leader's
    /// line some lengths ahead of himself (abreast of it while he's ahead of station), which brings him onto the line
    /// without snaking, turning round or cutting across the leader, and he
    /// rings the leader's speed plus what closes the gap along that line in <see cref="SimSettings.FollowCloseS"/>,
    /// within <see cref="SimSettings.FollowMaxSpeedDiffKn"/> and what his plant makes.</summary>
    public static (double CourseDeg, double SpeedKn) Follow(ShipState own, in Sighting leader, double distanceM, SimSettings s) =>
        Station(own, leader, distanceM > 0 ? distanceM : s.FollowDistanceLengths * own.Handling.LengthM, 0, s);

    /// <summary>Station: as <see cref="Follow"/>, for a station <paramref name="asternM"/> astern of the other ship and
    /// <paramref name="abeamM"/> to its starboard (negative: port), in its own frame. The line he joins runs through the
    /// station along the other ship's heading.</summary>
    public static (double CourseDeg, double SpeedKn) Station(ShipState own, in Sighting leader, double asternM, double abeamM, SimSettings s)
    {
        var h = own.Handling;
        double length = h.LengthM;
        var lf = leader.Forward;
        var lup = leader.Position.Normalized();
        var station = Surface(leader.Position - lf * asternM + Vec3d.Cross(lf, lup) * abeamM);
        double behind = Vec3d.Dot(station - own.Position, lf);   // positive: astern of station, close up

        // the point steered for is some lengths ahead of him along the leader's line, never behind him: a follower
        // ahead of station drops back by speed, it doesn't turn round. Astern of station it's on the line; well ahead of
        // station it's abreast of him, so he runs parallel and doesn't cut across the leader; between, a blend
        double along = Math.Max(-behind, 0) + s.FollowLookaheadLengths * length;
        var rel = own.Position - station;
        var up = station.Normalized();
        var cross = rel - lf * Vec3d.Dot(rel, lf) - up * Vec3d.Dot(rel, up);
        double join = Math.Clamp(1 + behind / (2 * length), 0, 1);
        var aim = Surface(station + lf * along + cross * (1 - join));
        double course = Earth.Bearing(own.Position, aim);

        // the speed: the leader's, what closes the gap in the closing time, less what's closing it already (damped:
        // his engines answer in minutes, so ringing on the gap alone overshoots, and down a line the swings grow); far
        // astern of station, all she makes
        double leaderKn = leader.SpeedMs / Handling.MsPerKnot;
        double closing = own.Speed * Vec3d.Dot(own.Forward, lf) - leader.SpeedMs;   // m/s the gap shrinks at
        if (behind > s.StationFarM)
            return (course, Flank(h));
        double extra = Math.Clamp((behind / s.FollowCloseS - s.FollowSpeedDamping * closing) / Handling.MsPerKnot,
            -s.FollowMaxSpeedDiffKn, s.FollowMaxSpeedDiffKn);
        double kn = Math.Clamp(leaderKn + extra, 0, Flank(h));
        return (course, kn);
    }

    /// <summary>Track: keep station <paramref name="distanceM"/> astern of the leader along the track he steamed (a
    /// <see cref="Trail"/>), not along his heading: in line ahead each ship turns where the one ahead turned, as a
    /// battle line turns in succession. He finds where he is along the track, steers the track's heading
    /// <see cref="SimSettings.FollowTrackLookaheadLengths"/> of his lengths further along it, less a correction toward
    /// it for how far he's off it a little on (where he is and the rate he closes it), and rings as <see cref="Station"/> does, the gap measured along the track. False (and nothing) when he's more than
    /// <see cref="SimSettings.FollowTrackJoinLengths"/> lengths off the track or astern of all of it: he joins the line
    /// the plain way first.</summary>
    public static bool Track(ShipState own, in Sighting leader, Trail trail, double distanceM, SimSettings s, out double courseDeg, out double speedKn)
    {
        (courseDeg, speedKn) = (0, 0);
        var h = own.Handling;
        double length = h.LengthM;
        var p = own.Position;

        // where he is along the track: the nearest point on it, walked back from the leader
        var a = leader.Position;
        double along = 0, best = double.MaxValue, at = 0;
        var tangent = leader.Forward;
        int atK = 0;
        for (int k = 0; k < trail.Count; k++)
        {
            var b = trail.Newest(k);
            var ab = b - a;
            double len = ab.Length;
            if (len > 1e-6)
            {
                double t = Math.Clamp(Vec3d.Dot(p - a, ab) / (len * len), 0, 1);
                double d = (p - (a + ab * t)).Length;
                if (d < best)
                    (best, at, tangent, atK) = (d, along + t * len, ab * (-1 / len), k);   // steaming from the older point to the newer
                along += len;
            }
            a = b;
        }
        if (best > s.FollowTrackJoinLengths * length || at >= along - 1)
            return false;
        // on the track: what lies further back than him and a margin is the leader's old wake, walked every second for
        // nothing (docs/battle/plan.md step 2.7c: 512 points a follower, 8% of the sim); it's dropped
        trail.KeepNewest(atK + 2 + (int)Math.Ceiling((s.FollowTrackJoinLengths * length + 2000) / s.FollowTrackStepM));

        // he steers the track's own heading a little ahead of him (where his helm will have answered), so he puts his
        // helm over where the ship ahead did and draws its arc, and corrects toward the track by how far he's off it
        // (steering for a point ahead on the track instead cuts inside every bend: his helm answers late)
        double aim = Math.Max(at - s.FollowTrackLookaheadLengths * length, 0);
        var (ahead, dir) = TangentBack(leader.Position, leader.Forward, trail, aim);
        var near = PointBack(leader.Position, trail, at);
        var starboard = Vec3d.Cross(tangent, p.Normalized());
        double off = Vec3d.Dot(p - near, starboard);   // positive: to starboard of the track
        off += s.FollowTrackCrossLeadOverT * h.T(own.Speed, s) * own.Speed * Vec3d.Dot(own.Forward, starboard);   // where he'll be at the rate he's going
        courseDeg = Earth.Heading(ahead, dir) - Math.Clamp(off * s.FollowTrackCrossDegPerM, -s.FollowTrackCrossMaxDeg, s.FollowTrackCrossMaxDeg);
        courseDeg = ((courseDeg % 360) + 360) % 360;

        double behind = at - distanceM;   // positive: astern of station along the track, close up
        double leaderKn = leader.SpeedMs / Handling.MsPerKnot;
        double closing = own.Speed * Vec3d.Dot(own.Forward, tangent) - leader.SpeedMs;
        double extra = Math.Clamp((behind / s.FollowCloseS - s.FollowSpeedDamping * closing) / Handling.MsPerKnot,
            -s.FollowMaxSpeedDiffKn, s.FollowMaxSpeedDiffKn);
        speedKn = Math.Clamp(leaderKn + extra, 0, Flank(h));
        return true;
    }

    /// <summary>The point a distance back along a track from its newest end (the leader himself), and the direction the
    /// track was steamed there (from the older point to the newer; the leader's own heading at his end).</summary>
    static (Vec3d At, Vec3d Dir) TangentBack(Vec3d from, Vec3d forward, Trail trail, double metres)
    {
        var a = from;
        for (int k = 0; k < trail.Count; k++)
        {
            var b = trail.Newest(k);
            var ab = b - a;
            double len = ab.Length;
            if (len > 1e-6)
            {
                if (len >= metres)
                    return (Surface(a + ab * (metres / len)), ab * (-1 / len));
                metres -= len;
            }
            a = b;
        }
        return (a, forward);
    }

    /// <summary>The point a distance back along a track from its newest end (the leader himself).</summary>
    static Vec3d PointBack(Vec3d from, Trail trail, double metres)
    {
        var a = from;
        for (int k = 0; k < trail.Count; k++)
        {
            var b = trail.Newest(k);
            double len = (b - a).Length;
            if (len >= metres && len > 1e-6)
                return Surface(a + (b - a) * (metres / len));
            metres -= len;
            a = b;
        }
        return a;
    }

    /// <summary>Shadow: keep the other ship at a range, on the bearing from it he's on now (in its own frame), by the
    /// same station keeping as a formation's. Closer, he opens; further, he closes; at the range he matches it.</summary>
    public static (double CourseDeg, double SpeedKn) Shadow(ShipState own, in Sighting target, double rangeM, SimSettings s)
    {
        var (astern, abeam) = ShadowStation(own, target, rangeM);
        return Station(own, target, astern, abeam, s);
    }

    /// <summary>The station a shadower keeps: at the range, on the bearing from the target he's on now (in its frame),
    /// metres astern and to starboard.</summary>
    public static (double AsternM, double AbeamM) ShadowStation(ShipState own, in Sighting target, double rangeM)
    {
        var up = target.Position.Normalized();
        var rel = own.Position - target.Position;
        rel -= up * Vec3d.Dot(rel, up);
        var starboard = Vec3d.Cross(target.Forward, up);
        double astern = -Vec3d.Dot(rel, target.Forward), abeam = Vec3d.Dot(rel, starboard);
        double len = Math.Sqrt(astern * astern + abeam * abeam);
        if (len < 1)
            (astern, abeam, len) = (rangeM, 0, rangeM);   // on top of it: astern, out of its way
        return (astern * rangeM / len, abeam * rangeM / len);
    }

    /// <summary>Station keeping for a coarse ship, deciding once a coarse step (docs/battle/plan.md step 2.8): steer for
    /// where the station will be <paramref name="horizonS"/> seconds from now (the other ship holding its course and
    /// speed; <see cref="SimSettings.CoarseRendezvousSteps"/> steps, so a change of speed that takes more than one step
    /// isn't overshot) at the speed that gets there by then. In station that's the other ship's course and speed, so it
    /// closes in a few steps; the fine tick's controllers, made for a second, swing at a minute. Ahead of where the station will be, he doesn't turn round: he runs on its heading, closing sideways, and
    /// drops back by speed. Within <see cref="SimSettings.FollowMaxSpeedDiffKn"/> of the other ship's speed and what his
    /// plant makes.</summary>
    public static (double CourseDeg, double SpeedKn) Rendezvous(ShipState own, in Sighting leader, double asternM, double abeamM, double horizonS, SimSettings s)
    {
        var h = own.Handling;
        var lf = leader.Forward;
        var lup = leader.Position.Normalized();
        var starboard = Vec3d.Cross(lf, lup);
        var then = Surface(leader.Position + lf * (leader.SpeedMs * horizonS - asternM) + starboard * abeamM);
        var d = then - own.Position;
        double along = Vec3d.Dot(d, lf), across = Vec3d.Dot(d, starboard);
        double least = 0.3 * Math.Max(leader.SpeedMs * horizonS, h.LengthM);   // never aimed behind him
        double heading = Earth.Heading(leader.Position, lf);
        double course = heading + Math.Atan2(across, Math.Max(along, least)) * 180 / Math.PI;
        double leaderKn = leader.SpeedMs / Handling.MsPerKnot;
        double kn = Math.Sqrt(Math.Max(along, 0) * Math.Max(along, 0) + across * across) / horizonS / Handling.MsPerKnot;
        kn = Math.Clamp(Math.Clamp(kn, leaderKn - s.FollowMaxSpeedDiffKn, leaderKn + s.FollowMaxSpeedDiffKn), 0, Flank(h));
        return (((course % 360) + 360) % 360, kn);
    }

    /// <summary>Run: turn directly away from the other ship and ring flank speed.</summary>
    public static (double CourseDeg, double SpeedKn) Run(ShipState own, in Sighting from)
    {
        var p = own.Position;
        double course = Earth.Bearing(p, p + (p - from.Position));
        return (course, Flank(own.Handling));
    }

    /// <summary>Fight (docs/battle/fighting.md 4.2c): the course that keeps the enemy on the beam, the side the main
    /// battery fights from, turned <paramref name="closeDeg"/> in toward him beyond the band (<paramref name="rangeM"/>,
    /// ± <paramref name="bandShare"/> of it) and as far out inside it. <paramref name="side"/> is which side the enemy is
    /// kept on (1 starboard, -1 port); 0 picks the one nearer the ship's heading, and it's returned so the captain keeps
    /// it. Beyond the band he turns in further when that angle wouldn't close on him by <paramref name="minCloseKn"/>
    /// (an enemy running away eats what it closes), as far as a stern chase. The speed is his full speed.</summary>
    public static (double CourseDeg, double SpeedKn) Fight(ShipState own, in Sighting enemy, double rangeM, double bandShare, double closeDeg,
        double minCloseKn, ref int side)
    {
        var p = own.Position;
        double bearing = Earth.Bearing(p, enemy.Position), range = Earth.Distance(p, enemy.Position);
        double off = range > rangeM * (1 + bandShare) ? 90 - closeDeg : range < rangeM * (1 - bandShare) ? 90 + closeDeg : 90;
        if (range > rangeM * (1 + bandShare))
        {
            // his speed away along the line of sight, and the angle off it that closes on that by the least wanted
            var away = Earth.Forward(enemy.Position, Earth.Bearing(enemy.Position, p) + 180);
            double opening = Vec3d.Dot(enemy.Forward, away) * enemy.SpeedMs;
            // at the speed she makes (her trials' top she may not, fouled or loaded), half her top while working up to it
            double v = Math.Max(own.Speed, 0.5 * own.Handling.Class.Motion.MaxSpeedKn * Handling.MsPerKnot);
            double need = (opening + minCloseKn * Handling.MsPerKnot) / v;
            off = need >= 1 ? 0 : Math.Min(off, double.RadiansToDegrees(Math.Acos(Math.Max(need, -1))));
        }
        if (side == 0)
        {
            double heading = own.HeadingDeg;
            side = Math.Abs(Wrap(bearing - off - heading)) <= Math.Abs(Wrap(bearing + off - heading)) ? 1 : -1;
        }
        double course = bearing - side * off;
        return (((course % 360) + 360) % 360, own.Handling.Class.Motion.MaxSpeedKn);
    }

    static double Wrap(double d) => d - 360 * Math.Round(d / 360);

    /// <summary>The most the ship makes: its speed at the plant's overload rating.</summary>
    public static double Flank(Handling h) => h.Class.SpeedAt(h.MaxPowerW / Handling.WattsPerShp);

    static Vec3d Surface(Vec3d p) => p.Normalized() * Earth.Radius;
}
