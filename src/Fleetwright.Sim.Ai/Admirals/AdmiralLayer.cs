using Fleetwright.Sim.Ai.Captains;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai.Admirals;

/// <summary>The admirals (docs/battle/plan.md steps 2.4 and 2.6): one per group, the layer above the captains. An
/// admiral holds his group's order (a course and speed, a route, or a behaviour on another unit) and hands it down to
/// his guide's captain as his own order, at once; when the guide changes (it left, sank or was detached) he hands it
/// to the next. Every second, before his captains think, he gives each other ship of the line its station from the
/// group's formation (<see cref="GroupSystem.StationOf"/>), only when it changed, and limits his guide's speed to what
/// the slowest of the line makes, less a margin to close up with. The limit is an order too, so the speed asked of the
/// group is kept and nothing is re-rung after the fact.
/// <para>The player's order to the group gathers back into the line the ships that were only nudged out of it;
/// pinned ones stay out. Ships that left their group or its line get their captains back, and ships that are gone
/// leave their group. He commands his own ships, so he reads them as they are; other ships he'd see through the
/// picture.</para></summary>
public sealed class AdmiralLayer(GroupSystem groups, CaptainLayer captains) : IAiLayer
{
    /// <summary>One admiral's order for his group, kept to hand to a new guide.</summary>
    struct Squadron
    {
        public int GroupId;
        public int HandedTo;            // the guide his order went to, 0 for none yet
        public int LimitTo;             // the guide his limit went to
        public double LimitKn;
        public CaptainOrder Order;      // None: nothing ordered, the guide keeps what it has
        public double CourseDeg, RudderDeg;   // NaN course: the guide's own (after a route was cleared)
        public double AskedKn;          // NaN: none
        public int Target;              // a unit
        public double DistanceM, AbeamM;
        public long[] Route;            // packed lat/lons, RouteCount of them
        public int RouteCount;
        public bool Loop;
    }

    Squadron[] squadrons = new Squadron[8];
    int count;
    readonly List<int> scratch = [];

    public bool Active => groups.Count > 0 || count > 0 || AnySuperiors();

    public int PeriodMs(in AiContext ctx)
    {
        for (int k = 0; k < groups.Count; k++)
        {
            var line = groups[k].Line;
            for (int i = 0; i < line.Count; i++)
                if (ctx.Ships.SlotOf(line[i]) is int slot and >= 0 && !ctx.Ships.OnRails(slot))
                    return ctx.Settings.AdmiralPeriodMs;
        }
        return ctx.Settings.AiRailsPeriodMs;
    }

    /// <summary>The speed asked of a group (NaN if none) and the limit its guide was given (0: none).</summary>
    public (double AskedKn, double LimitKn) SpeedOf(int groupId)
    {
        int i = Find(groupId);
        return i >= 0 ? (squadrons[i].AskedKn, squadrons[i].LimitKn) : (double.NaN, 0);
    }

    /// <summary>An order to a group, as a command (<see cref="UnitOrders"/>).</summary>
    public void Order(in AiContext ctx, in Command c)
    {
        var u = new Unit(c.Entity);
        if (groups.Get(u.GroupId) is not { } g)
            return;
        var code = (AiCommand)c.Code;
        if (code is AiCommand.Resume or AiCommand.Detach or AiCommand.SpeedLimit)
            return;   // nobody commands a group from above yet (2.6b)
        if ((OrderSource)c.B == OrderSource.Player)
            Gather(ctx, g);

        ref var s = ref Get(g.GroupId);
        int guide = g.Guide, lead = ctx.Ships.SlotOf(guide);
        switch (code)
        {
            case AiCommand.Course:
                (s.Order, s.CourseDeg, s.RudderDeg, s.RouteCount, s.Loop) = (CaptainOrder.Course, c.X, c.Y, 0, false);
                break;
            case AiCommand.Speed:
                if (s.Order is not (CaptainOrder.Course or CaptainOrder.Route))
                    (s.Order, s.CourseDeg, s.RudderDeg) = (CaptainOrder.Course, double.NaN, 0);
                s.AskedKn = c.X;
                break;
            case AiCommand.Hold:
                (s.Order, s.CourseDeg, s.RudderDeg, s.RouteCount, s.Loop) = (CaptainOrder.Course, double.NaN, 0, 0, false);
                s.AskedKn = lead >= 0 ? ctx.Ships[lead].SpeedOrderKn : double.NaN;
                break;
            case AiCommand.ClearRoute:
                (s.Order, s.CourseDeg, s.RudderDeg, s.RouteCount, s.Loop) = (CaptainOrder.Course, double.NaN, 0, 0, false);
                break;
            case AiCommand.AddWaypoint:
                if (s.Order != CaptainOrder.Route)
                    (s.RouteCount, s.Loop) = (0, false);
                s.Order = CaptainOrder.Route;
                s.Route ??= new long[8];
                if (s.RouteCount == s.Route.Length)
                    Array.Resize(ref s.Route, s.Route.Length * 2);
                s.Route[s.RouteCount++] = c.A;
                break;
            case AiCommand.LoopRoute:
                s.Loop = c.X != 0;
                break;
            case AiCommand.Follow or AiCommand.Station or AiCommand.Run:
                s.Order = code switch { AiCommand.Follow => CaptainOrder.Follow, AiCommand.Station => CaptainOrder.Station, _ => CaptainOrder.Run };
                (s.Target, s.DistanceM, s.AbeamM) = ((int)c.A, c.X, c.Y);
                break;
            default:
                throw new ArgumentException($"unknown AI command {c.Code}");
        }
        if (lead < 0)
            return;   // every ship detached (or the guide just sank): the order waits for a guide
        Limit(ctx, g, ref s);
        if (s.HandedTo != guide)
            HandOver(ctx, ref s, guide);   // everything he holds, this order with it
        else
            ctx.Issue(c with { Entity = guide, B = (int)OrderSource.Superior });
    }

    public void Think(in AiContext ctx)
    {
        var ships = ctx.Ships;

        // ships no longer at sea leave their groups
        scratch.Clear();
        for (int k = 0; k < groups.Count; k++)
        {
            var members = groups[k].Members;
            for (int i = 0; i < members.Count; i++)
                if (ships.SlotOf(members[i]) < 0)
                    scratch.Add(members[i]);
        }
        foreach (int m in scratch)
            ctx.Issue(GroupSystem.Remove(m));

        // groups that are gone take their admirals' orders with them
        for (int i = count - 1; i >= 0; i--)
            if (groups.Get(squadrons[i].GroupId) == null)
                Remove(i);

        for (int k = 0; k < groups.Count; k++)   // indexed: a foreach over the interfaces would allocate
        {
            var g = groups[k];
            int guide = g.Guide;
            if (guide == 0)
                continue;
            ref var s = ref Get(g.GroupId);
            if (s.HandedTo != guide)
                HandOver(ctx, ref s, guide);
            Limit(ctx, g, ref s);
            var line = g.Line;
            for (int i = 1; i < line.Count; i++)
            {
                var (target, astern, abeam) = GroupSystem.StationOf(g.Formation, g.SpacingM, line, i);
                if (!captains.Holds(line[i], CaptainOrder.Station, target, astern, abeam))
                    ctx.Issue(ctx.Orders.Station(Unit.Ship(line[i]), Unit.Ship(target), astern, abeam, OrderSource.Superior));
            }
        }

        // captains still holding a superior's order for a ship no longer in a line: theirs again
        for (int i = captains.Count - 1; i >= 0; i--)
        {
            int e = captains.SuperiorsAt(i);
            if (e != 0 && !groups.InLine(e))
                captains.Release(ctx, e);
        }
    }

    /// <summary>The player's order to a group is to all of it: the ships only nudged out of its line come back in.</summary>
    void Gather(in AiContext ctx, GroupSystem.Group g)
    {
        scratch.Clear();
        for (int i = 0; i < g.Members.Count; i++)
            if (g.DetachmentAt(i) == Detachment.Nudged)
                scratch.Add(g.Members[i]);
        foreach (int m in scratch)
            ctx.Issue(GroupSystem.Detach(m, Detachment.InLine));
    }

    /// <summary>A new guide takes everything the group was ordered, and the old one's limit goes.</summary>
    void HandOver(in AiContext ctx, ref Squadron s, int guide)
    {
        int old = s.HandedTo;
        s.HandedTo = guide;
        if (old != 0 && old == s.LimitTo)
        {
            ctx.Issue(ctx.Orders.SpeedLimit(Unit.Ship(old), 0));
            (s.LimitTo, s.LimitKn) = (0, 0);
        }
        var o = ctx.Orders;
        var to = Unit.Ship(guide);
        const OrderSource Up = OrderSource.Superior;
        switch (s.Order)
        {
            case CaptainOrder.None:
                ctx.Issue(o.Hold(to, Up));   // nothing ordered: the guide keeps what it has, out of any station
                break;
            case CaptainOrder.Course:
                if (double.IsNaN(s.CourseDeg))
                    ctx.Issue(o.Hold(to, Up));
                else
                    ctx.Issue(o.Course(to, s.CourseDeg, s.RudderDeg, Up));
                break;
            case CaptainOrder.Route:
                ctx.Issue(o.ClearRoute(to, Up));
                for (int i = 0; i < s.RouteCount; i++)
                    ctx.Issue(o.AddWaypoint(to, s.Route[i], Up));
                if (s.Loop)
                    ctx.Issue(o.LoopRoute(to, true, Up));
                break;
            case CaptainOrder.Follow:
                ctx.Issue(o.Follow(to, new Unit(s.Target), s.DistanceM, Up));
                break;
            case CaptainOrder.Station:
                ctx.Issue(o.Station(to, new Unit(s.Target), s.DistanceM, s.AbeamM, Up));
                break;
            case CaptainOrder.Run:
                ctx.Issue(o.Run(to, new Unit(s.Target), Up));
                break;
        }
        if (!double.IsNaN(s.AskedKn))
            ctx.Issue(o.Speed(to, s.AskedKn, Up));
    }

    /// <summary>The guide's limit: what the slowest of the line makes, less a margin to close up with; none for a
    /// ship alone. Given only when it changes.</summary>
    void Limit(in AiContext ctx, GroupSystem.Group g, ref Squadron s)
    {
        var line = g.Line;
        double cap = 0;
        if (line.Count > 1)
        {
            double slowest = double.MaxValue;
            for (int i = 0; i < line.Count; i++)
                if (ctx.Ships.SlotOf(line[i]) is int m and >= 0)
                    slowest = Math.Min(slowest, ctx.Ships[m].Handling.Class.Motion.MaxSpeedKn);
            cap = Math.Max(slowest - ctx.Settings.GroupSpeedMarginKn, 0.1);
        }
        int guide = g.Guide;
        if (s.LimitTo == guide && s.LimitKn == cap)
            return;
        if (s.LimitTo != 0 && s.LimitTo != guide)
            ctx.Issue(ctx.Orders.SpeedLimit(Unit.Ship(s.LimitTo), 0));
        (s.LimitTo, s.LimitKn) = (guide, cap);
        ctx.Issue(ctx.Orders.SpeedLimit(Unit.Ship(guide), cap));
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
        for (int i = 0; i < count; i++)
        {
            ref readonly var s = ref squadrons[i];
            h.Add(s.GroupId);
            h.Add(s.HandedTo);
            h.Add(s.LimitTo);
            h.Add(s.LimitKn);
            h.Add((int)s.Order);
            h.Add(s.CourseDeg);
            h.Add(s.RudderDeg);
            h.Add(s.AskedKn);
            h.Add(s.Target);
            h.Add(s.DistanceM);
            h.Add(s.AbeamM);
            h.Add(s.RouteCount);
            for (int k = 0; k < s.RouteCount; k++)
                h.Add(s.Route[k]);
            h.Add(s.Loop);
        }
    }

    public void Fill(ShipsView v)
    {
    }

    /// <summary>A group's admiral, made (nothing ordered yet) if it has none.</summary>
    ref Squadron Get(int groupId)
    {
        int i = Find(groupId);
        if (i >= 0)
            return ref squadrons[i];
        if (count == squadrons.Length)
            Array.Resize(ref squadrons, squadrons.Length * 2);
        i = count;
        while (i > 0 && squadrons[i - 1].GroupId > groupId)
        {
            squadrons[i] = squadrons[i - 1];
            i--;
        }
        squadrons[i] = new Squadron { GroupId = groupId, CourseDeg = double.NaN, AskedKn = double.NaN };
        count++;
        return ref squadrons[i];
    }

    int Find(int groupId)
    {
        int lo = 0, hi = count - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            int id = squadrons[mid].GroupId;
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
        Array.Copy(squadrons, i + 1, squadrons, i, count - i - 1);
        squadrons[--count] = default;
    }
}
