using Fleetwright.Sim.Ai.Captains;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai.Admirals;

/// <summary>The admirals' manoeuvres (docs/battle/command.md 5.1b): a group's line turned together, every ship at once, or
/// the rear ship first (the battle turn-away). The formation is changed at the start to what the ships will be in once
/// they've turned (<see cref="GroupSystem.TurnTogether"/>: the line stays where it lies, now on a bearing from the new
/// heading); then each ship is given the new course (through 16 points, a quarter at a time, so she turns the way the
/// signal said), at the speed the guide had, and nobody in the tree is given a station until every ship is round
/// (<see cref="SimSettings.ManoeuvreDoneDeg"/>, or <see cref="SimSettings.ManoeuvreMaxMs"/>). Then the group holds the new
/// course, and the stations are given on the new line. A turn the admiral makes on his own (the turn-away from
/// torpedoes) keeps his order; turned back, it's handed on again. A turn in succession is a course to a line ahead: its
/// ships turn where the guide turned.</summary>
public sealed partial class AdmiralLayer
{
    /// <summary>Starts a turn together of a group's line through an angle (+ to starboard). <paramref name="keep"/>: his
    /// own turn, his order stays; else the group's order becomes the new course.</summary>
    void StartTurn(in AiContext ctx, GroupSystem.Group g, Admiral a, double deg, bool rearFirst, bool keep)
    {
        int lead = ctx.Ships.SlotOf(groups.GuideShip(g));
        if (lead < 0)
            return;
        var guide = ctx.Ships[lead];

        // who turns when: rear first, by their stations astern of the guide as they lie now
        groups.LineOffsets(g, line);
        int n = line.Count;
        if (a.TurnShips.Length < n)
            a.TurnShips = new int[Math.Max(n, 2 * a.TurnShips.Length)];
        Ensure(n);
        for (int i = 0; i < n; i++)
            (keys[i], items[i]) = (rearFirst ? -line[i].AsternM : i, line[i].Ship);
        Array.Sort(keys, items, 0, n);
        Array.Copy(items, a.TurnShips, n);

        (a.Turning, a.TurnCount, a.TurnDeg, a.TurnRearFirst, a.TurnStartMs) = (true, n, deg, rearFirst, ctx.NowMs);
        a.TurnCourse = Wrap360(guide.HeadingDeg + deg);
        a.TurnKn = guide.SpeedOrderKn;
        ctx.Issue(GroupSystem.TurnTogether(g.GroupId, deg));
        if (!keep)
            (a.Order, a.CourseDeg, a.RudderDeg, a.RouteCount, a.Loop, a.Watch) = (CaptainOrder.Course, a.TurnCourse, 0, 0, false, false);
        TurnStep(ctx, g, a);
    }

    /// <summary>A turn together, each think: the ships whose time has come given the new course (a quarter of the way
    /// at a time past 150°, so each turns the way the signal said), and the speed; done when every one is round, or the
    /// time's up.</summary>
    void TurnStep(in AiContext ctx, GroupSystem.Group g, Admiral a)
    {
        var s = ctx.Settings;
        var ships = ctx.Ships;
        long since = ctx.NowMs - a.TurnStartMs;
        int due = a.TurnRearFirst ? 1 + (int)(since / Math.Max(1, (long)(s.BattleTurnStepS * 1000))) : a.TurnCount;
        bool round = true;
        for (int k = 0; k < a.TurnCount; k++)
        {
            int e = a.TurnShips[k];
            if (ships.SlotOf(e) is not (int slot and >= 0) || groups.UnitOf(e) != groups.TopOf(g))
                continue;   // gone, or out of the line
            if (k >= due)
            {
                round = false;
                continue;
            }
            var own = ships[slot];
            double off = Wrap180(a.TurnCourse - own.HeadingDeg);
            if (Math.Abs(off) > s.ManoeuvreDoneDeg)
                round = false;
            double want = Math.Abs(off) > 150 ? Wrap360(own.HeadingDeg + Math.Sign(a.TurnDeg) * 90) : a.TurnCourse;
            var (order, _, whose) = captains.Of(e);
            if (order != CaptainOrder.Course || whose != Whose.Superior || own.Helm != Helm.Course ||
                Math.Abs(Wrap180(want - own.CourseOrderDeg)) > s.CaptainCourseDeadbandDeg)
                ctx.Issue(ctx.Orders.Course(Unit.Ship(e), want, 0, OrderSource.Superior));
            if (!(Math.Abs(captains.SpeedOf(e).AskedKn - a.TurnKn) <= s.CaptainSpeedDeadbandKn))   // NaN too: none asked
                ctx.Issue(ctx.Orders.Speed(Unit.Ship(e), a.TurnKn, OrderSource.Superior));
        }
        if (round || since > s.ManoeuvreMaxMs)
            EndTurn(ctx, g, a);
    }

    /// <summary>The turn's done: his order handed to the guide again (the new course, or the one he kept), and the
    /// stations given on the new line by the think that follows.</summary>
    void EndTurn(in AiContext ctx, GroupSystem.Group g, Admiral a)
    {
        a.Turning = false;
        if (a.Order == CaptainOrder.Course && double.IsNaN(a.AskedKn))
            a.AskedKn = a.TurnKn;   // the speed the line turned at, kept
        var to = GuideUnit(g);
        if (to.Id != 0)
        {
            a.HandedTo = 0;   // handed afresh: every ship had his own course for the turn
            HandOver(ctx, a, to);
        }
    }

    /// <summary>The turn-away from torpedoes (docs/battle/command.md 5.1b), for a line that's no force's (its force turns),
    /// isn't a flotilla with torpedoes left (its role: it counter-attacks) and isn't on the conn or the player's helm
    /// (<see cref="HelmByHand"/>), with doctrine's TurnAwayDeg: when it sees
    /// an attack coming (<see cref="TorpedoThreat"/>)
    /// it turns together away from it through those degrees, holding the course it comes out on while his order (Fight,
    /// say) waits; once it's seen none for <see cref="SimSettings.TurnBackS"/> it turns back together and his order is
    /// handed on again. Whether a turn was started (the think gives no stations then).</summary>
    bool TurnAway(in AiContext ctx, GroupSystem.Group g, Admiral a, int lead)
    {
        var guide = ctx.Ships[lead];
        if (groups.InForceLine(g) || a.Order == CaptainOrder.Screen || a.Conn || HelmByHand(a))   // its force turns; a screen counter-attacks; the conn and the player's helm hold on
            return false;
        double deg = doctrine.OfGroup(g, guide.Side, DoctrineField.TurnAwayDeg);
        if (!a.Away && (deg <= 0 || Roles.Of(Unit.Group(g.GroupId)) == UnitRole.Flotilla && CaptainLayer.TorpedoesLeft(ctx, guide, lead)))
            return false;
        bool threat = TorpedoThreat(ctx, g, guide, out double bearing, out _);
        long now = ctx.NowMs;
        if (a.Away)
        {
            if (threat)
                a.ThreatMs = now;
            if (threat || now - a.ThreatMs < (long)(ctx.Settings.TurnBackS * 1000))
                return false;
            a.Away = false;
            StartTurn(ctx, g, a, -a.AwayDeg, rearFirst: false, keep: true);
            return true;
        }
        if (!threat)
            return false;
        double turn = Wrap180(bearing - guide.HeadingDeg) >= 0 ? -deg : deg;   // coming from starboard: away to port
        (a.Away, a.AwayDeg, a.ThreatMs) = (true, turn, now);
        StartTurn(ctx, g, a, turn, rearFirst: false, keep: true);
        a.AwayCourse = a.TurnCourse;
        return true;
    }

    /// <summary>Whether a group steers by the player's helm order (a course, a route, a deploy, holding on: by hand),
    /// which holds it to his course against its admiral's own manoeuvres (docs/map-ui/battle-hud.md decision 6).</summary>
    static bool HelmByHand(Admiral a) => a.Whose == Whose.Player && a.Order is CaptainOrder.Course or CaptainOrder.Route;

    /// <summary>Whether a line sees a torpedo attack coming: an enemy its navy takes for a torpedo craft within
    /// <see cref="SimSettings.TorpedoThreatM"/> of a ship of the line and running in at her faster than
    /// <see cref="SimSettings.TorpedoThreatRunInMs"/> (its own speed toward her: a line steaming at a boat lying ahead, or
    /// passing one that crosses, isn't attacked), or a ship of it combing a track her lookouts saw; and the bearing
    /// from the guide it comes from (the nearest such craft, <paramref name="craft"/>; else the nearest enemy in sight). Never
    /// without a bearing.</summary>
    bool TorpedoThreat(in AiContext ctx, GroupSystem.Group? g, ShipState guide, out double bearing, out int craft)
    {
        (bearing, craft) = (0, 0);
        var s = ctx.Settings;
        var ships = ctx.Ships;
        var pic = ctx.World.Sightings.Of(guide.Side);
        if (pic == null)
            return false;
        if (g != null)
            groups.LineOffsets(g, line);
        else
        {
            line.Clear();
            line.Add((guide.Entity, 0, 0));   // a ship alone
        }
        double reach = 0;
        bool tracks = false;
        foreach (var (ship, astern, abeam) in line)
        {
            reach = Math.Max(reach, Math.Sqrt(astern * astern + abeam * abeam));
            tracks |= captains.Combing(ship, ctx.NowMs);
        }
        double craftD = double.MaxValue, anyD = double.MaxValue, anyBearing = 0;
        foreach (int i in seen.Near(ctx.World, guide.Side, guide.Position, reach + s.TorpedoThreatM))
        {
            var q = seen.Position(guide.Side, i);
            double d = (q - guide.Position).Length;
            if (d < anyD)
                (anyD, anyBearing) = (d, Earth.Bearing(guide.Position, q));
            if (!TakenForAttacker(ctx, pic[i], guide))
                continue;
            var v = seen.Velocity(guide.Side, i);
            foreach (var (ship, _, _) in line)
            {
                if (ships.SlotOf(ship) is not (int m and >= 0))
                    continue;
                var own = ships[m];
                var to = own.Position - q;
                double r = to.Length;
                if (r > s.TorpedoThreatM || r < 1 || Vec3d.Dot(v, to) / r < s.TorpedoThreatRunInMs)
                    continue;
                if (d < craftD)
                    (craftD, bearing, craft) = (d, Earth.Bearing(guide.Position, q), pic[i].Entity);
                break;
            }
        }
        if (craftD < double.MaxValue)
            return true;
        bearing = anyBearing;
        return tracks && anyD < double.MaxValue;
    }

    /// <summary>Whether a contact is taken for a torpedo threat to a line: once the class it's taken for is made out, an
    /// attacker against the line's guide's (<see cref="Knowledge.MatchupJudge.AttackerAgainst(in AiContext, MatchupProfile, MatchupProfile, byte)"/>,
    /// docs/battle/admirals-orders.md O7c); before, by its type (a destroyer, or smaller).</summary>
    bool TakenForAttacker(in AiContext ctx, in Contact c, ShipState guide)
    {
        if (c.Level < ContactLevel.Class || c.TakenFor < 0 || ctx.World.Ships.Registry.Get(c.TakenFor) is not { } taken)
            return c.Type is ShipType.Destroyer or ShipType.Small;
        var m = Judge.Matchups;
        return Judge.AttackerAgainst(ctx, m.Profile(ctx.World, taken), m.Profile(ctx.World, guide.Handling.Class), guide.Side);
    }

    /// <summary>Whether a group is turning together, or the line it's in is.</summary>
    bool Manoeuvring(GroupSystem.Group g)
    {
        for (var at = g; ;)
        {
            if (Find(at.GroupId) is int i and >= 0 && admirals[i].Turning)
                return true;
            if (!groups.InForceLine(at) || groups.Get(at.Parent) is not { } up)
                return false;
            at = up;
        }
    }

    static double Wrap180(double d) => d - 360 * Math.Round(d / 360);

    static double Wrap360(double d) => ((d % 360) + 360) % 360;
}
