using Fleetwright.Sim.Ai.Captains;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai.Admirals;

/// <summary>A group's screen (docs/battle/command.md 5.1b; the task proper, with its ends and hand-back, is 5.1c's): it
/// keeps <see cref="SimSettings.ScreenM"/> (or the distance ordered) off the screened unit's guide, on a bearing its admiral
/// chooses (docs/battle/admirals-orders.md O5), placed again only when that moves its guide's station more than
/// <see cref="SimSettings.ScreenMoveM"/>: with no enemy in sight of the screened guide within
/// <see cref="SimSettings.FightCompareM"/>, dead ahead (on its bows, split); with one, toward the nearest, unless he judges
/// his screen alone no match for the enemy in sight (under doctrine's bar by <see cref="SimSettings.ScreenMarginShare"/>,
/// back over it by as much): then away from him, on the disengaged side (split, at its van and its rear), out of the line
/// of fire and ready to go round its ends.
/// <para>A screen splits (docs/battle/admirals-orders.md O1b): a squadron of two ships or more told to screen is
/// divided first, into a force of two divisions (<see cref="GroupSystem.Divide"/>), so each half is a squadron that sails
/// and fights as one. A force's line splits in two: its first half of squadrons (its guide's) on one side of that
/// bearing, the rest on the other, each <see cref="SimSettings.ScreenSplitDeg"/> (or the split ordered) off it and
/// spread on the arc that far out, the force's spacing apart from the bearing outward, each squadron keeping its own
/// station on the screened guide, its ships in their formation about it. Each time it's placed, the guide's half takes
/// the side of the bearing its guide is on, so each half moves the least (the van division stays the van). Ordered in
/// one body, the group keeps its formation about its guide's station.</para>
/// <para>The counter-attack: while enemy torpedo craft close on the screened unit (<see cref="TorpedoThreat"/>, as its
/// own turn-away sees them) the half on their side of the bearing (all of a screen in one body) fights the nearest, the
/// other half keeping its station, and goes back to its station <see cref="SimSettings.TurnBackS"/> after the last. It
/// doesn't take up doctrine's Engage (screening is its fight); its guns fire at will as always.</para></summary>
public sealed partial class AdmiralLayer
{
    void ScreenStep(in AiContext ctx, GroupSystem.Group g, Admiral a)
    {
        if (a.Order != CaptainOrder.Screen)
            return;
        var s = ctx.Settings;
        long now = ctx.NowMs;
        var to = GuideUnit(g);
        var o = ctx.Orders;
        const OrderSource Up = OrderSource.Superior;
        var screened = new Unit(a.Target);
        var sg = screened.IsGroup ? groups.Get(screened.GroupId) : null;
        int ship = sg != null ? groups.GuideShip(sg) : screened.Entity;
        if (ctx.Ships.SlotOf(ship) is not (int slot and >= 0))
        {
            // nobody left to screen: it holds what it has
            (a.Order, a.CourseDeg, a.RudderDeg, a.ScreenFight, a.ScreenFightSide, a.ScreenOn) = (CaptainOrder.Course, double.NaN, 0, 0, 0, 0);
            ctx.Issue(o.Hold(to, Up));
            return;
        }
        var guide = ctx.Ships[slot];

        TorpedoThreat(ctx, sg, guide, out double craftBearing, out int craft);
        if (craft != 0)
        {
            if (a.ScreenOn != ship && Split(g, a))
                Place(ctx, g, a, guide, force: true);   // placed first: a counter-attack needs to know the halves' sides
            a.ScreenThreatMs = now;
            int side = Split(g, a) ? (Wrap180(craftBearing - guide.HeadingDeg - a.ScreenAxisDeg) >= 0 ? 1 : -1) : 0;
            if (a.ScreenFight != craft || a.ScreenFightSide != side)
            {
                bool guideFought = a.ScreenFight != 0 && (a.ScreenFightSide == 0 || a.ScreenFightSide == a.ScreenSide);
                (a.ScreenFight, a.ScreenFightSide) = (craft, side);
                if (side == 0 || side == a.ScreenSide)
                    ctx.Issue(o.Fight(to, Unit.Ship(craft), 0, Up));   // the counter-attack, the guide's half in it
                else if (guideFought)
                    ctx.Issue(o.Station(to, Unit.Ship(a.ScreenOn), a.ScreenAstern, a.ScreenAbeam, Up));
            }
            return;
        }
        if (a.ScreenFight != 0)
        {
            if (now - a.ScreenThreatMs < (long)(s.TurnBackS * 1000))
                return;
            (a.ScreenFight, a.ScreenFightSide) = (0, 0);   // beaten off: back to the stations
            Place(ctx, g, a, guide, force: true);
            return;
        }
        Place(ctx, g, a, guide, force: a.ScreenOn != ship);
    }

    /// <summary>The screen placed on the bearing of the nearest enemy in sight of the screened guide (ahead of it if none):
    /// its guide given its station when it's new (<paramref name="force"/>) or moved past <see cref="SimSettings.ScreenMoveM"/>;
    /// the rest of a split screen take theirs from the station loop (<see cref="ScreenMember"/>).</summary>
    void Place(in AiContext ctx, GroupSystem.Group g, Admiral a, ShipState guide, bool force)
    {
        double axis = 0;
        if (NearestEnemyBearing(ctx, guide) is double b)
            axis = Wrap180(b - guide.HeadingDeg + (Away(ctx, g, a, guide) ? 180 : 0));
        else
            a.ScreenAway = false;   // nothing in sight: on the bows
        var (oldAxis, oldSide) = (a.ScreenAxisDeg, a.ScreenSide);
        // the guide's half takes the side of the bearing its guide is on now: each half moves the least
        double on = ctx.Ships.SlotOf(groups.GuideShip(g)) is int m and >= 0 ? Wrap180(Earth.Bearing(guide.Position, ctx.Ships[m].Position) - guide.HeadingDeg) : axis + 1;
        (a.ScreenAxisDeg, a.ScreenSide) = (axis, Wrap180(on - axis) >= 0 ? 1 : -1);
        var (astern, abeam) = ScreenPlace(g, a, 0);
        double moved = Math.Sqrt((astern - a.ScreenAstern) * (astern - a.ScreenAstern) + (abeam - a.ScreenAbeam) * (abeam - a.ScreenAbeam));
        if (!force && moved <= ctx.Settings.ScreenMoveM)
        {
            (a.ScreenAxisDeg, a.ScreenSide) = (oldAxis, oldSide);   // not worth moving: the stations stand
            return;
        }
        (a.ScreenOn, a.ScreenAstern, a.ScreenAbeam) = (guide.Entity, astern, abeam);
        ctx.Issue(ctx.Orders.Station(GuideUnit(g), Unit.Ship(guide.Entity), astern, abeam, OrderSource.Superior));
    }

    /// <summary>Whether the screen keeps to the disengaged side: its group alone against the enemy in sight of the screened
    /// guide, judged against doctrine's bar with <see cref="SimSettings.ScreenMarginShare"/> either way.</summary>
    bool Away(in AiContext ctx, GroupSystem.Group g, Admiral a, ShipState screened)
    {
        double c = Judge.GroupAgainstNear(ctx, g, screened);
        double bar = Judge.Bars(groups.GuideShip(g), screened.Side).Accept, margin = ctx.Settings.ScreenMarginShare;
        if (!a.ScreenAway && c < bar / (1 + margin))
            a.ScreenAway = true;
        else if (a.ScreenAway && c > bar * (1 + margin))
            a.ScreenAway = false;
        return a.ScreenAway;
    }

    /// <summary>Whether a screen splits: a force of two or more groups in its line, and a split (a squadron is divided
    /// into one when it's told to screen).</summary>
    static bool Split(GroupSystem.Group g, Admiral a) => a.ScreenSplitDeg > 0 && g.IsForce && g.Line.Count >= 2;

    /// <summary>The i-th of a screen's line's station off the screened guide (astern, abeam), m: on the arc
    /// <see cref="Admiral.DistanceM"/> out, its half's split off the bearing screened on, spread the group's spacing apart
    /// from the bearing outward (no nearer it than half the split); in one body, the bearing itself.</summary>
    static (double Astern, double Abeam) ScreenPlace(GroupSystem.Group g, Admiral a, int i)
    {
        double deg = a.ScreenAxisDeg;
        if (Split(g, a))
        {
            int n = g.Line.Count, half = (n + 1) / 2;
            bool first = i < half;
            int m = first ? half : n - half, j = first ? i : i - half;
            double step = m > 1 ? Math.Min(g.SpacingM / a.DistanceM * 180 / Math.PI, a.ScreenSplitDeg / (m - 1)) : 0;
            deg += (first ? a.ScreenSide : -a.ScreenSide) * (a.ScreenSplitDeg + (j - (m - 1) / 2.0) * step);
        }
        double rad = deg * Math.PI / 180;
        return (-a.DistanceM * Math.Cos(rad), a.DistanceM * Math.Sin(rad));
    }

    /// <summary>A squadron of a split screen's line (the i-th, past its guide) given its place: the half counter-attacking
    /// fights, the rest keep their stations on the arc. False if the group isn't a split screen placed: its formation's
    /// station, then.</summary>
    bool ScreenMember(in AiContext ctx, GroupSystem.Group g, Admiral a, int i)
    {
        if (a.Order != CaptainOrder.Screen || a.ScreenOn == 0 || !Split(g, a))
            return false;
        int member = g.Line[i];
        int side = i < (g.Line.Count + 1) / 2 ? a.ScreenSide : -a.ScreenSide;
        if (a.ScreenFight != 0 && (a.ScreenFightSide == 0 || a.ScreenFightSide == side))
        {
            if (!HoldsFight(member, a.ScreenFight))
                ctx.Issue(ctx.Orders.Fight(Unit.Group(member), Unit.Ship(a.ScreenFight), 0, OrderSource.Superior));
            return true;
        }
        var (astern, abeam) = ScreenPlace(g, a, i);
        if (!Holds(member, a.ScreenOn, astern, abeam))
            ctx.Issue(ctx.Orders.Station(Unit.Group(member), Unit.Ship(a.ScreenOn), astern, abeam, OrderSource.Superior));
        return true;
    }

    /// <summary>Whether a group's admiral already holds his superior's order to fight this ship.</summary>
    bool HoldsFight(int groupId, int ship) =>
        Find(groupId) is int i and >= 0 && admirals[i] is { Whose: Whose.Superior, Order: CaptainOrder.Fight } a && a.Target == Unit.Ship(ship).Id;

    /// <summary>The bearing from a ship to the nearest enemy her navy holds in sight within
    /// <see cref="SimSettings.FightCompareM"/>, or null.</summary>
    double? NearestEnemyBearing(in AiContext ctx, ShipState from)
    {
        double best = double.MaxValue;
        double? bearing = null;
        foreach (int i in seen.Near(ctx.World, from.Side, from.Position, ctx.Settings.FightCompareM))
        {
            var q = seen.Position(from.Side, i);
            double d = (q - from.Position).LengthSquared;
            if (d < best)
                (best, bearing) = (d, Earth.Bearing(from.Position, q));
        }
        return bearing;
    }
}
