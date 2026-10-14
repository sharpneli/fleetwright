using Fleetwright.Sim.Ai.Captains;
using Fleetwright.Sim.Ai.Knowledge;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai.Admirals;

/// <summary>The admirals (docs/battle/plan.md steps 2.4 and 2.6): one per group, at every level above the captains: a
/// squadron's rear admiral commands its ships' captains, a force's admiral its groups' admirals, as many levels up as
/// forces nest. An admiral holds his group's order (a course and speed, a route, or a behaviour on another unit) and
/// hands it down to his guide (a ship's captain, or a group's admiral) as his own order, at once; when the guide changes
/// (it left, sank or was detached) he hands it to the next. Every second he gives each other member of his line its
/// station (<see cref="GroupSystem.StationOf"/>, <see cref="GroupSystem.StationOfGroup"/>), only when it changed, and
/// hands his guide two limits as orders: the speed the slowest ship of his line makes, less a margin to close up with
/// (or his superior's, if less), and the clearance from the coast his formation's half-width needs (or his superior's,
/// if more). The speed asked of the group is kept and nothing is re-rung after the fact.
/// <para><b>Who has a group:</b> a group in a force's line takes its admiral's orders, as a ship in a squadron's line
/// does its captain's: the player's order to it nudges it out of the line, an order to the force gathers it back,
/// <see cref="AiCommand.Detach"/> pins it out until <see cref="AiCommand.Resume"/>, a route the player gave it hands it
/// back at its end, and its superior's orders for it are dropped meanwhile, with a notice ("resume?") raised once. A
/// group's admiral raises one for his held members too when his own orders change.</para>
/// <para>Ships that left their group or its line get their captains back, groups that left their force their admirals,
/// and ships that are gone leave their group. He commands his own ships, so he reads them as they are; other ships he'd
/// see through the picture.</para></summary>
public sealed partial class AdmiralLayer(GroupSystem groups, CaptainLayer captains, SeenContacts seen, Doctrine doctrine) : IAiLayer
{
    /// <summary>One admiral: his group's order, kept to hand to a new guide, and who gave it.</summary>
    sealed class Admiral
    {
        public int GroupId;
        public OrderSource Source;
        public int HandedTo;            // the guide unit his order went to, 0 for none yet
        public int LimitTo, ClearTo;    // the guide units his limit and clearance went to
        public double LimitKn, ClearM;  // what he gave them (0: none)
        public double LimitAbove, ClearAbove;   // his superior's (0: none)
        public CaptainOrder Order;      // None: nothing ordered, the guide keeps what it has
        public double CourseDeg = double.NaN, RudderDeg;   // NaN course: the guide's own (after a route was cleared)
        public double AskedKn = double.NaN;   // NaN: none
        public int Target;              // a unit
        public double DistanceM, AbeamM;
        public long[] Route = [];       // packed lat/lons, RouteCount of them
        public int RouteCount;
        public bool Loop;
        public bool Watch;              // held by the player on a route: its end hands the group back
        public bool Asked;              // held by the player while his superior's orders for it changed: a notice
        public FireOrder Fire;          // his group's fire order (AdmiralLayer.Fire.cs), engaging FireTarget
        public int FireTarget;
        public bool Met;                // doctrine's Engage taken up in this encounter
        public bool Turning;            // turning his line together (AdmiralLayer.Manoeuvres.cs)
        public bool TurnRearFirst;
        public double TurnDeg, TurnCourse, TurnKn;
        public long TurnStartMs;
        public int[] TurnShips = new int[16];   // his line's ships in the order they turn, TurnCount of them (made with him)
        public int TurnCount;
        public bool Away;               // turned away from torpedoes, AwayDeg, on AwayCourse; his order waits
        public double AwayDeg, AwayCourse;
        public long ThreatMs;           // when he last saw the attack
    }

    Admiral[] admirals = new Admiral[8];
    int count;
    readonly List<int> scratch = [];
    readonly List<(int Ship, double AsternM, double AbeamM)> line = [];

    public bool Active => groups.Count > 0 || count > 0 || AnySuperiors();

    /// <summary>How many notices admirals have raised (a group's notice going up).</summary>
    public int Notices { get; private set; }

    public int PeriodMs(in AiContext ctx)
    {
        int period = ctx.Settings.AiRailsPeriodMs;
        for (int k = 0; k < groups.Count; k++)
        {
            if (groups[k].IsForce)
                continue;
            var members = groups[k].Line;
            for (int i = 0; i < members.Count; i++)
                if (ctx.Ships.SlotOf(members[i]) is int slot and >= 0)
                {
                    var tier = ctx.Ships.TierOf(slot);
                    if (tier == ClockTier.Fine)
                        return ctx.Settings.AdmiralPeriodMs;
                    if (tier == ClockTier.Coarse)
                        period = Math.Min(period, ctx.Settings.CoarseStepMs);
                }
        }
        return period;
    }

    /// <summary>A group's admiral's order, its target unit and who gave it; None if he has none.</summary>
    public (CaptainOrder Order, int Target, OrderSource Source) Of(int groupId) =>
        Find(groupId) is int i and >= 0 ? (admirals[i].Order, admirals[i].Target, admirals[i].Source) : (CaptainOrder.None, 0, OrderSource.Player);

    /// <summary>The speed asked of a group (NaN if none) and the limit its guide was given (0: none).</summary>
    public (double AskedKn, double LimitKn) SpeedOf(int groupId) =>
        Find(groupId) is int i and >= 0 ? (admirals[i].AskedKn, admirals[i].LimitKn) : (double.NaN, 0);

    /// <summary>The clearance from the coast a group's admiral gave its guide (0: none).</summary>
    public double ClearanceOf(int groupId) => Find(groupId) is int i and >= 0 ? admirals[i].ClearM : 0;

    /// <summary>Whether a group's admiral has a notice up: held by the player, his superior's orders for it changed.</summary>
    public bool IsAsked(int groupId) => Find(groupId) is int i and >= 0 && admirals[i].Asked;

    /// <summary>An order to a group, as a command (<see cref="UnitOrders"/>).</summary>
    public void Order(in AiContext ctx, in Command c)
    {
        var u = new Unit(c.Entity);
        if (groups.Get(u.GroupId) is not { } g)
            return;
        var code = (AiCommand)c.Code;
        var source = (OrderSource)c.B;
        var parent = g.Parent != 0 ? groups.Get(g.Parent) : null;
        var detached = parent?.DetachmentOf(g.GroupId) ?? Detachment.InLine;
        var a = Get(g.GroupId);

        switch (code)
        {
            case AiCommand.FireAtWill or AiCommand.HoldFire or AiCommand.Engage:
                FireOrdered(ctx, g, a, c);   // the guns: the line keeps its stations
                return;
            case AiCommand.Form:
                ctx.Issue(GroupSystem.SetFormation(g.GroupId, (Formation)c.A, c.X, c.Y));   // the line keeps its orders
                return;
        }

        switch (code)
        {
            case AiCommand.Resume:
                if (parent != null && detached != Detachment.InLine)
                    ctx.Issue(GroupSystem.DetachGroup(g.GroupId, Detachment.InLine));   // its force's admiral gives it its station
                a.Asked = false;
                return;
            case AiCommand.Detach:
                if (parent == null || detached == Detachment.Pinned)
                    return;
                ctx.Issue(GroupSystem.DetachGroup(g.GroupId, Detachment.Pinned));
                Release(ctx, g, a);   // it holds what it was doing, the player's now
                return;
            case AiCommand.SpeedLimit:
                a.LimitAbove = c.X;
                Limit(ctx, g, a);
                return;
            case AiCommand.Clearance:
                a.ClearAbove = c.X;
                Clear(ctx, g, a);
                return;
        }

        if (source == OrderSource.Superior && parent != null && detached != Detachment.InLine)
        {
            Ask(a);   // the player has it: its superior's order waits for the hand-back, and is given again then
            return;
        }
        if (source == OrderSource.Player && parent != null && detached == Detachment.InLine)
        {
            ctx.Issue(GroupSystem.DetachGroup(g.GroupId, Detachment.Nudged));   // the player takes it out of the line
            detached = Detachment.Nudged;
            (a.LimitAbove, a.ClearAbove) = (0, 0);   // its superior's limits go with its superior
        }
        if (source == OrderSource.Superior)
            a.Asked = false;   // in its superior's hands again
        if (source == OrderSource.Player)
            Gather(ctx, g);
        a.Source = source;
        (a.Turning, a.Away) = (false, false);   // a new order ends a turn under way, and a turn-away

        if (code == AiCommand.TurnTogether)
        {
            StartTurn(ctx, g, a, c.X, c.Y != 0, keep: false);
            AskHeld(g);
            return;
        }

        int lead = ctx.Ships.SlotOf(groups.GuideShip(g));
        switch (code)
        {
            case AiCommand.Course:
                (a.Order, a.CourseDeg, a.RudderDeg, a.RouteCount, a.Loop) = (CaptainOrder.Course, c.X, c.Y, 0, false);
                break;
            case AiCommand.Speed:
                if (a.Order is not (CaptainOrder.Course or CaptainOrder.Route))
                    (a.Order, a.CourseDeg, a.RudderDeg) = (CaptainOrder.Course, double.NaN, 0);
                a.AskedKn = c.X;
                break;
            case AiCommand.Hold:
                (a.Order, a.CourseDeg, a.RudderDeg, a.RouteCount, a.Loop) = (CaptainOrder.Course, double.NaN, 0, 0, false);
                a.AskedKn = lead >= 0 ? ctx.Ships[lead].SpeedOrderKn : double.NaN;
                break;
            case AiCommand.ClearRoute:
                (a.Order, a.CourseDeg, a.RudderDeg, a.RouteCount, a.Loop) = (CaptainOrder.Course, double.NaN, 0, 0, false);
                break;
            case AiCommand.AddWaypoint:
                if (a.Order != CaptainOrder.Route)
                    (a.RouteCount, a.Loop) = (0, false);
                a.Order = CaptainOrder.Route;
                if (a.RouteCount == a.Route.Length)
                    Array.Resize(ref a.Route, Math.Max(8, a.Route.Length * 2));
                a.Route[a.RouteCount++] = c.A;
                break;
            case AiCommand.LoopRoute:
                a.Loop = c.X != 0;
                break;
            case AiCommand.Follow or AiCommand.Station or AiCommand.Run or AiCommand.Shadow or AiCommand.Fight:
                a.Order = code switch
                {
                    AiCommand.Follow => CaptainOrder.Follow,
                    AiCommand.Station => CaptainOrder.Station,
                    AiCommand.Shadow => CaptainOrder.Shadow,
                    AiCommand.Fight => CaptainOrder.Fight,
                    _ => CaptainOrder.Run,
                };
                (a.Target, a.DistanceM, a.AbeamM) = ((int)c.A, c.X, c.Y);
                break;
            default:
                throw new ArgumentException($"unknown AI command {c.Code}");
        }
        a.Watch = a.Order == CaptainOrder.Route && source == OrderSource.Player && detached == Detachment.Nudged;
        AskHeld(g);   // his orders changed: his members the player holds are asked back

        var to = GuideUnit(g);
        if (to.Id == 0 || lead < 0)
            return;   // every member detached (or the guide just sank): the order waits for a guide
        Limit(ctx, g, a);
        Clear(ctx, g, a);
        if (a.HandedTo != to.Id)
            HandOver(ctx, a, to);   // everything he holds, this order with it
        else
            ctx.Issue(c with { Entity = to.Id, B = (int)OrderSource.Superior });
    }

    public void Think(in AiContext ctx)
    {
        var ships = ctx.Ships;

        // ships no longer at sea leave their groups
        scratch.Clear();
        for (int k = 0; k < groups.Count; k++)
        {
            if (groups[k].IsForce)
                continue;
            var members = groups[k].Members;
            for (int i = 0; i < members.Count; i++)
                if (ships.SlotOf(members[i]) < 0)
                    scratch.Add(members[i]);
        }
        foreach (int m in scratch)
            ctx.Issue(GroupSystem.Remove(m));

        // groups that are gone take their admirals' orders with them
        for (int i = count - 1; i >= 0; i--)
            if (groups.Get(admirals[i].GroupId) == null)
                Remove(i);

        for (int k = 0; k < groups.Count; k++)   // indexed: a foreach over the interfaces would allocate
        {
            var g = groups[k];
            var a = Get(g.GroupId);
            int lead = ships.SlotOf(groups.GuideShip(g));
            if (a.Watch && lead >= 0)
            {
                // a route the player gave a group out of its force's line: at its end, back to its force
                var s = ships[lead];
                if (s.RouteLoop)
                    a.Watch = false;
                else if (s.Helm != Helm.Route)
                {
                    (a.Watch, a.Order, a.CourseDeg) = (false, CaptainOrder.Course, double.NaN);
                    if (g.Parent != 0 && groups.Get(g.Parent)?.DetachmentOf(g.GroupId) == Detachment.Nudged)
                        ctx.Issue(GroupSystem.DetachGroup(g.GroupId, Detachment.InLine));
                }
            }
            var to = GuideUnit(g);
            if (to.Id == 0 || lead < 0)
                continue;
            if (a.Turning)
                TurnStep(ctx, g, a);
            if (Manoeuvring(g))
            {
                // turning together: every ship has her course; no stations, no new guide's orders until it's done
                if (a.Fire == FireOrder.Engage)
                    HandFire(ctx, g, a, fresh: false);
                Limit(ctx, g, a);
                Clear(ctx, g, a);
                continue;
            }
            if (a.HandedTo != to.Id)
                HandOver(ctx, a, to);
            TakeUpStanding(ctx, g, a, lead);
            if (TurnAway(ctx, g, a, lead))
                continue;   // turning together: no stations until it's round
            if (a.Fire == FireOrder.Engage)
                HandFire(ctx, g, a, fresh: false);
            Limit(ctx, g, a);
            Clear(ctx, g, a);
            var members = g.Line;
            for (int i = 1; i < members.Count; i++)
            {
                if (!g.IsForce)
                {
                    var (target, astern, abeam) = GroupSystem.StationOf(g, i);
                    if (!captains.Holds(members[i], CaptainOrder.Station, target, astern, abeam))
                        ctx.Issue(ctx.Orders.Station(Unit.Ship(members[i]), Unit.Ship(target), astern, abeam, OrderSource.Superior));
                }
                else
                {
                    var (target, astern, abeam) = groups.StationOfGroup(g, i);
                    if (target != 0 && !Holds(members[i], target, astern, abeam))
                        ctx.Issue(ctx.Orders.Station(Unit.Group(members[i]), Unit.Ship(target), astern, abeam, OrderSource.Superior));
                }
            }
        }

        // captains and admirals still holding a superior's order for a unit no longer in a line: theirs again
        for (int i = captains.Count - 1; i >= 0; i--)
        {
            int e = captains.SuperiorsAt(i);
            if (e != 0 && !groups.InLine(e))
                captains.Release(ctx, e);
        }
        for (int i = 0; i < count; i++)
            if (admirals[i].Source == OrderSource.Superior && groups.Get(admirals[i].GroupId) is { } g && !groups.InForceLine(g))
                Release(ctx, g, admirals[i]);
    }

    /// <summary>Whether a group's admiral already holds his superior's order to keep this station on a ship.</summary>
    bool Holds(int groupId, int ship, double astern, double abeam) =>
        Find(groupId) is int i and >= 0 && admirals[i] is { Source: OrderSource.Superior, Order: CaptainOrder.Station } a &&
        a.Target == Unit.Ship(ship).Id && a.DistanceM == astern && a.AbeamM == abeam;

    /// <summary>The unit a group's orders go to: its guide ship's captain, or its guide group's admiral.</summary>
    static Unit GuideUnit(GroupSystem.Group g) => g.Guide == 0 ? default : g.IsForce ? Unit.Group(g.Guide) : Unit.Ship(g.Guide);

    /// <summary>The player's order to a group is to all of it: the members only nudged out of its line come back in.</summary>
    void Gather(in AiContext ctx, GroupSystem.Group g)
    {
        scratch.Clear();
        for (int i = 0; i < g.Members.Count; i++)
            if (g.DetachmentAt(i) == Detachment.Nudged)
                scratch.Add(g.Members[i]);
        foreach (int m in scratch)
            ctx.Issue(g.IsForce ? GroupSystem.DetachGroup(m, Detachment.InLine) : GroupSystem.Detach(m, Detachment.InLine));
    }

    /// <summary>A group's orders changed: each member the player holds is asked back.</summary>
    void AskHeld(GroupSystem.Group g)
    {
        for (int i = 0; i < g.Members.Count; i++)
            if (g.DetachmentAt(i) != Detachment.InLine)
            {
                if (g.IsForce)
                    Ask(Get(g.Members[i]));
                else
                    captains.Ask(g.Members[i]);
            }
    }

    void Ask(Admiral a)
    {
        if (a.Asked)
            return;
        a.Asked = true;
        Notices++;
    }

    /// <summary>A group no longer its superior's (it left its force, or was detached): it holds the course and speed
    /// ordered now, and its superior's limits go. Not an order: the admiral finds he has nobody above him.</summary>
    void Release(in AiContext ctx, GroupSystem.Group g, Admiral a)
    {
        int lead = ctx.Ships.SlotOf(groups.GuideShip(g));
        var to = GuideUnit(g);
        if (a.Order is CaptainOrder.Follow or CaptainOrder.Run or CaptainOrder.Station or CaptainOrder.Shadow or CaptainOrder.Fight or CaptainOrder.None && lead >= 0 && to.Id != 0)
        {
            (a.Order, a.CourseDeg, a.RudderDeg, a.RouteCount, a.Loop) = (CaptainOrder.Course, double.NaN, 0, 0, false);
            a.AskedKn = ctx.Ships[lead].SpeedOrderKn;
            ctx.Issue(ctx.Orders.Hold(to, OrderSource.Superior));
        }
        (a.LimitAbove, a.ClearAbove, a.Source, a.Asked) = (0, 0, OrderSource.Player, false);
        if (to.Id != 0)
        {
            Limit(ctx, g, a);
            Clear(ctx, g, a);
        }
    }

    /// <summary>A new guide takes everything the group was ordered, and the old one's limits go.</summary>
    void HandOver(in AiContext ctx, Admiral a, Unit to)
    {
        int old = a.HandedTo;
        a.HandedTo = to.Id;
        if (old != 0 && old == a.LimitTo)
        {
            ctx.Issue(ctx.Orders.SpeedLimit(new Unit(old), 0));
            (a.LimitTo, a.LimitKn) = (0, 0);
        }
        if (old != 0 && old == a.ClearTo)
        {
            ctx.Issue(ctx.Orders.Clearance(new Unit(old), 0));
            (a.ClearTo, a.ClearM) = (0, 0);
        }
        var o = ctx.Orders;
        const OrderSource Up = OrderSource.Superior;
        if (a.Away)
        {
            ctx.Issue(o.Course(to, a.AwayCourse, 0, Up));   // turned away from torpedoes: his order waits
            return;
        }
        switch (a.Order)
        {
            case CaptainOrder.None:
                ctx.Issue(o.Hold(to, Up));   // nothing ordered: the guide keeps what it has, out of any station
                break;
            case CaptainOrder.Course:
                ctx.Issue(double.IsNaN(a.CourseDeg) ? o.Hold(to, Up) : o.Course(to, a.CourseDeg, a.RudderDeg, Up));
                break;
            case CaptainOrder.Route:
                ctx.Issue(o.ClearRoute(to, Up));
                for (int i = 0; i < a.RouteCount; i++)
                    ctx.Issue(o.AddWaypoint(to, a.Route[i], Up));
                if (a.Loop)
                    ctx.Issue(o.LoopRoute(to, true, Up));
                break;
            case CaptainOrder.Follow:
                ctx.Issue(o.Follow(to, new Unit(a.Target), a.DistanceM, Up));
                break;
            case CaptainOrder.Station:
                ctx.Issue(o.Station(to, new Unit(a.Target), a.DistanceM, a.AbeamM, Up));
                break;
            case CaptainOrder.Run:
                ctx.Issue(o.Run(to, new Unit(a.Target), Up));
                break;
            case CaptainOrder.Shadow:
                ctx.Issue(o.Shadow(to, new Unit(a.Target), a.DistanceM, Up));
                break;
            case CaptainOrder.Fight:
                ctx.Issue(o.Fight(to, new Unit(a.Target), a.DistanceM, Up));
                break;
        }
        if (!double.IsNaN(a.AskedKn))
            ctx.Issue(o.Speed(to, a.AskedKn, Up));
    }

    /// <summary>The guide's speed limit: what the slowest ship of the line makes, down the tree, less a margin to close
    /// up with (none for a ship alone), or the superior's if less. Given only when it changes.</summary>
    void Limit(in AiContext ctx, GroupSystem.Group g, Admiral a)
    {
        groups.LineOffsets(g, line);
        double cap = 0;
        if (line.Count > 1)
        {
            double slowest = double.MaxValue;
            for (int i = 0; i < line.Count; i++)
                if (ctx.Ships.SlotOf(line[i].Ship) is int m and >= 0)
                    slowest = Math.Min(slowest, ctx.Ships[m].Handling.Class.Motion.MaxSpeedKn);
            cap = Math.Max(slowest - ctx.Settings.GroupSpeedMarginKn, 0.1);
        }
        if (a.LimitAbove > 0)
            cap = cap > 0 ? Math.Min(cap, a.LimitAbove) : a.LimitAbove;
        int to = GuideUnit(g).Id;
        if (a.LimitTo == to && a.LimitKn == cap)
            return;
        if (a.LimitTo != 0 && a.LimitTo != to)
            ctx.Issue(ctx.Orders.SpeedLimit(new Unit(a.LimitTo), 0));
        (a.LimitTo, a.LimitKn) = (to, cap);
        ctx.Issue(ctx.Orders.SpeedLimit(new Unit(to), cap));
    }

    /// <summary>The guide's clearance from the coast: the farthest station abeam of it, down the tree, or the
    /// superior's if more. Given only when it changes.</summary>
    void Clear(in AiContext ctx, GroupSystem.Group g, Admiral a)
    {
        groups.LineOffsets(g, line);
        double width = 0;
        for (int i = 1; i < line.Count; i++)
            width = Math.Max(width, Math.Abs(line[i].AbeamM));
        width = Math.Max(width, a.ClearAbove);
        int to = GuideUnit(g).Id;
        if (a.ClearTo == to && a.ClearM == width)
            return;
        if (a.ClearTo != 0 && a.ClearTo != to)
            ctx.Issue(ctx.Orders.Clearance(new Unit(a.ClearTo), 0));
        (a.ClearTo, a.ClearM) = (to, width);
        ctx.Issue(ctx.Orders.Clearance(new Unit(to), width));
    }

    bool AnySuperiors()
    {
        for (int i = 0; i < captains.Count; i++)
            if (captains.SuperiorsAt(i) != 0)
                return true;
        return false;
    }

    public void Hash(ref StateHash h)
    {
        h.Add(count);
        h.Add(Notices);
        for (int i = 0; i < count; i++)
        {
            var a = admirals[i];
            h.Add(a.GroupId);
            h.Add((int)a.Source);
            h.Add(a.HandedTo);
            h.Add(a.LimitTo);
            h.Add(a.LimitKn);
            h.Add(a.LimitAbove);
            h.Add(a.ClearTo);
            h.Add(a.ClearM);
            h.Add(a.ClearAbove);
            h.Add((int)a.Order);
            h.Add(a.CourseDeg);
            h.Add(a.RudderDeg);
            h.Add(a.AskedKn);
            h.Add(a.Target);
            h.Add(a.DistanceM);
            h.Add(a.AbeamM);
            h.Add(a.RouteCount);
            for (int k = 0; k < a.RouteCount; k++)
                h.Add(a.Route[k]);
            h.Add(a.Loop);
            h.Add(a.Watch);
            h.Add(a.Asked);
            h.Add((int)a.Fire);
            h.Add(a.FireTarget);
            h.Add(a.Met);
            if (a.Turning)
            {
                h.Add(a.TurnRearFirst);
                h.Add(a.TurnDeg);
                h.Add(a.TurnCourse);
                h.Add(a.TurnKn);
                h.Add(a.TurnStartMs);
                for (int k = 0; k < a.TurnCount; k++)
                    h.Add(a.TurnShips[k]);
            }
            if (a.Away)
            {
                h.Add(a.AwayDeg);
                h.Add(a.AwayCourse);
                h.Add(a.ThreatMs);
            }
        }
    }

    public void Fill(Snapshot s)
    {
        var v = s.Groups;
        for (int k = 0; k < v.Count; k++)
            v.Asked[k] = IsAsked(v.Id[k]);
    }

    /// <summary>A group's admiral, made (nothing ordered yet) if it has none: the only allocation, once per group.</summary>
    Admiral Get(int groupId)
    {
        int i = Find(groupId);
        if (i >= 0)
            return admirals[i];
        if (count == admirals.Length)
            Array.Resize(ref admirals, admirals.Length * 2);
        i = count;
        while (i > 0 && admirals[i - 1].GroupId > groupId)
        {
            admirals[i] = admirals[i - 1];
            i--;
        }
        admirals[i] = new Admiral { GroupId = groupId };
        count++;
        return admirals[i];
    }

    int Find(int groupId)
    {
        int lo = 0, hi = count - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            int id = admirals[mid].GroupId;
            if (id == groupId)
                return mid;
            if (id < groupId)
                lo = mid + 1;
            else
                hi = mid - 1;
        }
        return -1;
    }

    void Remove(int i)
    {
        Array.Copy(admirals, i + 1, admirals, i, count - i - 1);
        admirals[--count] = null!;
    }
}
