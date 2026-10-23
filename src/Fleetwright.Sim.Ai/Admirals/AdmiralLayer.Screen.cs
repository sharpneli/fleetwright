using Fleetwright.Sim.Ai.Captains;
using Fleetwright.Sim.Ai.Knowledge;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai.Admirals;

/// <summary>Screens (docs/battle/command.md 5.1b; docs/battle/admirals-orders.md O1c, O5). Screen is a position, not a
/// formation: a squadron told to screen a unit keeps <see cref="SimSettings.ScreenM"/> (or the distance ordered) off
/// the screened unit's guide, on a station of that unit's <b>arc</b>, which every squadron screening it shares.
/// <para><b>The arc</b> (<see cref="Arcs"/>, once a think): its bearing is dead ahead of the screened unit with no enemy
/// in sight of it within <see cref="SimSettings.FightCompareM"/>; with one, toward the nearest, unless the screens
/// together are no match for the enemy in sight (the matchup, under doctrine's bar by
/// <see cref="SimSettings.ScreenMarginShare"/>, back over it by as much): then away from him, on the disengaged side. It
/// moves only when that moves its stations more than <see cref="SimSettings.ScreenMoveM"/>. One screen sits on the
/// bearing; two or more spread evenly to <see cref="SimSettings.ScreenArcDeg"/> either side of it (two: the usual four
/// points on either bow; three: either bow and dead ahead), taking the stations in the order they lie about it, so none
/// crosses another.</para>
/// <para><b>The counter-attack:</b> while enemy torpedo craft close on the screened unit (<see cref="TorpedoThreat"/>, as
/// its own turn-away sees them) the screen whose station is nearest their bearing (and then the one at it, while the attack
/// lasts) fights the nearest of them, the others
/// keeping their stations, and goes back to its station <see cref="SimSettings.TurnBackS"/> after the last.</para>
/// <para><b>A force told to screen</b> hands the order to each of its groups: its squadrons screen the unit, each on its
/// own station of the arc. <b>Organising is apart from it:</b> the order never divides a squadron. Its own admiral may
/// (<see cref="GroupSystem.Divide"/>): alone on its arc, its order (or task) not the player's by hand, with
/// <see cref="SimSettings.ScreenDivideShips"/> ships or more in its line, he divides it into two divisions so both bows are
/// covered, once an order. A screen doesn't take up doctrine's Engage (screening is its fight); its guns fire at will as always.</para></summary>
public sealed partial class AdmiralLayer
{
    /// <summary>A screened unit's arc, kept between thinks (its bearing and side have margins either way).</summary>
    struct Arc
    {
        public int Target;          // the unit screened
        public double AxisDeg;      // the bearing screened on, off the screened guide's bow
        public bool Away;           // on the disengaged side: the screens no match for the enemy in sight
        public bool Placed;
        public long BuiltMs;
        public int Craft, Fighter;  // enemy torpedo craft closing (0: none) and the screen counter-attacking them
    }

    Arc[] arcs = new Arc[4];
    int arcCount;
    readonly List<(int Target, int Group, double Angle)> screening = new(16);
    readonly MatchupSide arcOwn = new(), arcEnemy = new();
    static readonly IComparer<(int Target, int Group, double Angle)> ByUnit = Comparer<(int Target, int Group, double Angle)>.Create(
        (x, y) => x.Target != y.Target ? x.Target.CompareTo(y.Target) : x.Group.CompareTo(y.Group));
    static readonly IComparer<(int Target, int Group, double Angle)> ByAngle = Comparer<(int Target, int Group, double Angle)>.Create(
        (x, y) => x.Angle != y.Angle ? x.Angle.CompareTo(y.Angle) : x.Group.CompareTo(y.Group));

    /// <summary>Every squadron screening, by the unit it screens; each unit's arc judged once, its screens given their
    /// stations' bearings in the order they lie, the counter-attacker chosen; a screen alone on its arc divided by its
    /// admiral if it's big enough and his own to organise.</summary>
    void Arcs(in AiContext ctx)
    {
        var s = ctx.Settings;
        screening.Clear();
        for (int i = 0; i < count; i++)
        {
            var a = admirals[i];
            if (a.Order != CaptainOrder.Screen || groups.Get(a.GroupId) is not { IsForce: false } g)
                continue;
            if (ScreenedGuide(ctx, a) is not int on || ctx.Ships.SlotOf(groups.GuideShip(g)) is not (int mine and >= 0))
                continue;
            var them = ctx.Ships[on];
            screening.Add((a.Target, a.GroupId, Wrap180(Earth.Bearing(them.Position, ctx.Ships[mine].Position) - them.HeadingDeg)));
        }
        screening.Sort(ByUnit);
        int divide = 0;
        for (int start = 0; start < screening.Count;)
        {
            int target = screening[start].Target, end = start;
            while (end < screening.Count && screening[end].Target == target)
                end++;
            int n = end - start;
            var guide = ctx.Ships[ScreenedGuide(ctx, Get(screening[start].Group))!.Value];
            ref var arc = ref ArcOf(target);
            arc.BuiltMs = ctx.NowMs;

            // the bearing: ahead; toward the nearest enemy in sight; or away from him
            double axis = 0;
            bool away = false;
            if (NearestEnemyBearing(ctx, guide) is double b)
            {
                arcOwn.Clear();
                for (int k = start; k < end; k++)
                    if (groups.Get(screening[k].Group) is { } sg)
                        Judge.AddGroup(ctx.World, sg, arcOwn);
                arcEnemy.Clear();
                Judge.AddNear(ctx.World, guide.Side, guide.Position, s.FightCompareM, arcEnemy);
                double c = arcEnemy.Count == 0 ? double.PositiveInfinity : arcOwn.Count == 0 ? 0 : Judge.Judge(ctx, guide.Entity, guide.Side, guide.Position, arcOwn, arcEnemy).Confidence;
                double bar = Judge.Bars(guide.Entity, guide.Side).Accept, margin = s.ScreenMarginShare;
                away = arc.Away ? c <= bar * (1 + margin) : c < bar / (1 + margin);
                axis = Wrap180(b - guide.HeadingDeg + (away ? 180 : 0));
            }
            double moveDeg = s.ScreenMoveM / s.ScreenM * 180 / Math.PI;
            if (!arc.Placed || away != arc.Away || Math.Abs(Wrap180(axis - arc.AxisDeg)) > moveDeg)
                (arc.AxisDeg, arc.Away, arc.Placed) = (axis, away, true);

            // the stations, in the order the screens lie about the bearing
            for (int k = start; k < end; k++)
                screening[k] = screening[k] with { Angle = Wrap180(screening[k].Angle - arc.AxisDeg) };
            screening.Sort(start, n, ByAngle);
            for (int k = start; k < end; k++)
                Get(screening[k].Group).ScreenBearingDeg = Wrap180(arc.AxisDeg + (n == 1 ? 0 : -s.ScreenArcDeg + 2 * s.ScreenArcDeg * (k - start) / (n - 1)));

            // an attack: the screen whose station is nearest it counter-attacks
            var screened = new Unit(target);
            TorpedoThreat(ctx, screened.IsGroup ? groups.Get(screened.GroupId) : null, guide, out double craftBearing, out arc.Craft);
            // (the one counter-attacking keeps at it while the attack lasts)
            int fighting = arc.Fighter;
            arc.Fighter = 0;
            if (arc.Craft != 0)
            {
                double rel = craftBearing - guide.HeadingDeg, best = double.MaxValue;
                for (int k = start; k < end; k++)
                    if (screening[k].Group == fighting)
                        (best, arc.Fighter) = (-1, fighting);
                    else if (best >= 0 && Math.Abs(Wrap180(Get(screening[k].Group).ScreenBearingDeg - rel)) is var d && d < best)
                        (best, arc.Fighter) = (d, screening[k].Group);
            }

            // alone on its arc: its admiral may divide it, to cover both bows
            if (n == 1 && s.ScreenDivideShips > 0 && Get(screening[start].Group) is { Whose: not Whose.Player, ScreenDivided: false } lone &&
                Tasks?.ByHand(Unit.Group(lone.GroupId)) != true && groups.Get(lone.GroupId) is { } lg && lg.Line.Count >= s.ScreenDivideShips && arc.Craft == 0)
                divide = lone.GroupId;
            start = end;
        }
        // arcs nobody screens on any more go
        int kept = 0;
        for (int i = 0; i < arcCount; i++)
            if (arcs[i].BuiltMs == ctx.NowMs)
                arcs[kept++] = arcs[i];
        Array.Clear(arcs, kept, arcCount - kept);
        arcCount = kept;
        if (divide != 0 && groups.Get(divide) is { } whole)
        {
            Get(divide).ScreenDivided = true;   // once an order: a player who merges it back isn't overruled
            ctx.Issue(GroupSystem.Divide(divide, (whole.Members.Count + 1) / 2));   // a force of two divisions: each screens next think
        }
    }

    /// <summary>A squadron screening: on its station of the arc, or counter-attacking; a force hands the order to its groups
    /// (<see cref="ScreenHanded"/>).</summary>
    void ScreenStep(in AiContext ctx, GroupSystem.Group g, Admiral a)
    {
        if (a.Order != CaptainOrder.Screen)
            return;
        var s = ctx.Settings;
        long now = ctx.NowMs;
        var to = GuideUnit(g);
        var o = ctx.Orders;
        const OrderSource Up = OrderSource.Superior;
        if (ScreenedGuide(ctx, a) is not int slot)
        {
            // nobody left to screen: it holds what it has
            (a.Order, a.CourseDeg, a.RudderDeg, a.ScreenFight, a.ScreenOn) = (CaptainOrder.Course, double.NaN, 0, 0, 0);
            ctx.Issue(o.Hold(to, Up));
            return;
        }
        if (g.IsForce || FindArc(a.Target) is not int ai)
            return;   // a force's groups screen; a screen not yet on an arc is placed next think
        ref readonly var arc = ref arcs[ai];
        var guide = ctx.Ships[slot];
        if (arc.Craft != 0 && arc.Fighter == g.GroupId)
        {
            a.ScreenThreatMs = now;
            if (a.ScreenFight != arc.Craft)
            {
                a.ScreenFight = arc.Craft;
                ctx.Issue(o.Fight(to, Unit.Ship(arc.Craft), 0, Up));   // the counter-attack
            }
            return;
        }
        bool force = a.ScreenOn != guide.Entity;
        if (a.ScreenFight != 0)
        {
            if (now - a.ScreenThreatMs < (long)(s.TurnBackS * 1000))
                return;
            (a.ScreenFight, force) = (0, true);   // beaten off: back to its station
        }
        double rad = a.ScreenBearingDeg * Math.PI / 180;
        double astern = -a.DistanceM * Math.Cos(rad), abeam = a.DistanceM * Math.Sin(rad);
        double moved = Math.Sqrt((astern - a.ScreenAstern) * (astern - a.ScreenAstern) + (abeam - a.ScreenAbeam) * (abeam - a.ScreenAbeam));
        if (!force && moved <= s.ScreenMoveM)
            return;
        (a.ScreenOn, a.ScreenAstern, a.ScreenAbeam) = (guide.Entity, astern, abeam);
        ctx.Issue(o.Station(to, Unit.Ship(guide.Entity), astern, abeam, Up));
    }

    /// <summary>A group of a force told to screen (the i-th of its line, past its guide, which has it handed over): the
    /// screen handed down, so it takes its own station on the arc. False if the force isn't screening.</summary>
    bool ScreenHanded(in AiContext ctx, GroupSystem.Group g, Admiral a, int i)
    {
        if (!g.IsForce || a.Order != CaptainOrder.Screen)
            return false;
        int member = g.Line[i];
        if (!(Find(member) is int mi and >= 0 && admirals[mi] is { Whose: Whose.Superior, Order: CaptainOrder.Screen } m && m.Target == a.Target && m.DistanceM == a.DistanceM))
            ctx.Issue(ctx.Orders.Screen(Unit.Group(member), new Unit(a.Target), a.DistanceM, OrderSource.Superior));
        return true;
    }

    /// <summary>The slot of the ship guiding the unit a screen screens, or null if it's gone.</summary>
    int? ScreenedGuide(in AiContext ctx, Admiral a)
    {
        var screened = new Unit(a.Target);
        int ship = screened.IsGroup ? groups.Get(screened.GroupId) is { } sg ? groups.GuideShip(sg) : 0 : screened.Entity;
        return ctx.Ships.SlotOf(ship) is int slot and >= 0 ? slot : null;
    }

    ref Arc ArcOf(int target)
    {
        if (FindArc(target) is int i)
            return ref arcs[i];
        if (arcCount == arcs.Length)
            Array.Resize(ref arcs, arcs.Length * 2);
        int at = arcCount;
        while (at > 0 && arcs[at - 1].Target > target)
        {
            arcs[at] = arcs[at - 1];
            at--;
        }
        arcs[at] = new Arc { Target = target };
        arcCount++;
        return ref arcs[at];
    }

    int? FindArc(int target)
    {
        for (int i = 0; i < arcCount; i++)
            if (arcs[i].Target == target)
                return i;
        return null;
    }

    void HashArcs(ref StateHash h)
    {
        if (arcCount == 0)
            return;
        h.Add(arcCount);
        for (int i = 0; i < arcCount; i++)
        {
            ref readonly var arc = ref arcs[i];
            h.Add(arc.Target);
            h.Add(arc.AxisDeg);
            h.Add(arc.Away);
            h.Add(arc.Placed);
            h.Add(arc.BuiltMs);
            h.Add(arc.Craft);
            h.Add(arc.Fighter);
        }
    }

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
