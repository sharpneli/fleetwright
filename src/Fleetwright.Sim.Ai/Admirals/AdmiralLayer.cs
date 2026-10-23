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
/// group's admiral raises one for his held members too when his own orders change. The AI's own orders (a task's, the
/// side's commander's) take a group out of the line as <see cref="Detachment.Tasked"/>: not the player's, so no notice,
/// and the player's order takes it from its task.</para>
/// <para>Ships that left their group or its line get their captains back, groups that left their force their admirals,
/// and ships that are gone leave their group. He commands his own ships, so he reads them as they are; other ships he'd
/// see through the picture.</para></summary>
public sealed partial class AdmiralLayer(GroupSystem groups, CaptainLayer captains, SeenContacts seen, Doctrine doctrine) : IAiLayer
{
    /// <summary>One admiral: his group's order, kept to hand to a new guide, and who gave it.</summary>
    sealed class Admiral
    {
        public int GroupId;
        public Whose Whose;             // whose order the group is on: its own, its superior's, the player's by hand
        public int HandedTo;            // the guide unit his order went to, 0 for none yet
        public int LimitTo, ClearTo;    // the guide units his limit and clearance went to
        public double LimitKn, ClearM;  // what he gave them (0: none)
        public double LimitAbove, ClearAbove;   // his superior's (0: none)
        public CaptainOrder Order;      // None: nothing ordered, the guide keeps what it has
        public double CourseDeg = double.NaN, RudderDeg;   // NaN course: the guide's own (after a route was cleared)
        public double AskedKn = double.NaN;   // NaN: none
        public int Target;              // a unit
        public double DistanceM, AbeamM;
        public long[] Route = new long[8];   // packed lat/lons, RouteCount of them (room made with him: a first route allocates nothing)
        public int RouteCount;
        public bool Loop;
        public bool Watch;              // held by the player on a route: its end hands the group back
        public bool Asked;              // held by the player while his superior's orders for it changed: a notice
        public bool Conn;               // the player has the conn: nothing but the player's helm orders moves the line
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
        public AttackPhase Attack;      // his flotilla's attack (AdmiralLayer.Attack.cs) on AttackTarget
        public bool AttackOrdered;      // told to Attack (else by doctrine)
        public int AttackTarget, AttackShips, AttackSide;   // the ship; the boats it came in with; the side of him it gains (±1)
        public int AttackTubes;                             // the beam the boats' tubes are trained on (±90), 0 none yet
        public long AttackMs;           // when the phase began
        public int ScreenOn;            // screening (AdmiralLayer.Screen.cs): the ship his station was given on (0: none yet)
        public double ScreenAstern, ScreenAbeam;
        public int ScreenFight;         // the enemy torpedo craft he's counter-attacking (0: none)
        public long ScreenThreatMs;     // when he last saw one closing
        public double ScreenSplitDeg;   // each half this far off the bearing screened on (0: in one body)
        public double ScreenAxisDeg;    // the bearing screened on, off the screened guide's bow, as last placed
        public int ScreenSide;          // the side of that bearing his guide's half keeps (±1; 0: not placed yet)
        public int ScreenFightSide;     // the half counter-attacking (±1; 0: all of it)
        public bool ScreenAway;         // on the side away from the enemy: no match for him (O5)
        public long DeployMs;           // deploying into a line of battle since (AdmiralLayer.Deploy.cs; 0: not)
        public bool Deployed;           // deployed in this encounter
    }

    Admiral[] admirals = new Admiral[8];
    int count;
    readonly List<int> scratch = [];
    readonly List<(int Ship, double AsternM, double AbeamM)> line = [];

    public bool Active => groups.Count > 0 || count > 0 || AnySuperiors();

    /// <summary>The units' tasks: a withdrawing group doesn't take up doctrine's fight (set by the AI system).</summary>
    internal Tasks.TaskLayer? Tasks { get; set; }

    /// <summary>Who can fight whom (docs/battle/command.md 5.1g): whether a flotilla presses its attack home.</summary>
    internal Knowledge.MatchupJudge Judge { get; set; } = null!;

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

    /// <summary>A group's admiral's order, its target unit and whose it is; None if he has none.</summary>
    public (CaptainOrder Order, int Target, Whose Whose) Of(int groupId) =>
        Find(groupId) is int i and >= 0 ? (admirals[i].Order, admirals[i].Target, admirals[i].Whose) : (CaptainOrder.None, 0, Whose.Own);

    /// <summary>The speed asked of a group (NaN if none) and the limit its guide was given (0: none).</summary>
    public (double AskedKn, double LimitKn) SpeedOf(int groupId) =>
        Find(groupId) is int i and >= 0 ? (admirals[i].AskedKn, admirals[i].LimitKn) : (double.NaN, 0);

    /// <summary>The clearance from the coast a group's admiral gave its guide (0: none).</summary>
    public double ClearanceOf(int groupId) => Find(groupId) is int i and >= 0 ? admirals[i].ClearM : 0;

    /// <summary>Whether a group's admiral has a notice up: held by the player, his superior's orders for it changed.</summary>
    public bool IsAsked(int groupId) => Find(groupId) is int i and >= 0 && admirals[i].Asked;

    /// <summary>Whether a ship steams in a line the player has the conn of (docs/battle/command.md "The conn"): her
    /// squadron's, she in its line, or a force's above it, each group in its force's line.</summary>
    public bool LineConned(int ship)
    {
        if (!groups.InLine(ship) || groups.Of(ship) is not { } g)
            return false;
        while (true)
        {
            if (Find(g.GroupId) is int i and >= 0 && admirals[i].Conn)
                return true;
            if (g.Parent == 0 || groups.Get(g.Parent) is not { } up || up.DetachmentOf(g.GroupId) != Detachment.InLine)
                return false;
            g = up;
        }
    }

    /// <summary>An order to a group, as a command (<see cref="UnitOrders"/>).</summary>
    public void Order(in AiContext ctx, in Command c)
    {
        var u = new Unit(c.Entity);
        if (groups.Get(u.GroupId) is not { } g)
            return;
        var code = (AiCommand)c.Code;
        var from = OrderFrom.Of(c);
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
            case AiCommand.Conn when c.X == 0:
                a.Conn = false;   // the conn given back: its order stands, its admiral's judgement too
                return;
        }

        switch (code)
        {
            case AiCommand.Resume:
                if (parent != null && detached != Detachment.InLine)
                    ctx.Issue(GroupSystem.DetachGroup(g.GroupId, Detachment.InLine));   // its force's admiral gives it its station
                (a.Asked, a.Conn) = (false, false);
                if (a.Whose == Whose.Player)
                    a.Whose = Whose.Own;   // let go: its superior's station comes next think
                return;
            case AiCommand.Detach:
                if (parent == null || Chain.Pin(from, detached) is not { } pin)
                    return;
                ctx.Issue(GroupSystem.DetachGroup(g.GroupId, pin));
                Release(ctx, g, a);   // it holds what it was doing, the player's (or its task's) now
                if (from.Hand)
                    a.Whose = Whose.Player;
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

        if (parent != null)
        {
            // its superior's order waits while it's out of the line (the player has it: a notice; its task: none, it
            // waits for its end); the player's takes it out of the line or from its task, a task's out of the line
            if (Chain.Place(from, detached, out bool ask) is not { } place)
            {
                if (ask)
                    Ask(a);
                return;
            }
            if (place != detached)
            {
                if (detached == Detachment.InLine)
                    (a.LimitAbove, a.ClearAbove) = (0, 0);   // its superior's limits go with its superior
                ctx.Issue(GroupSystem.DetachGroup(g.GroupId, detached = place));
            }
        }
        var whose = from.Whose;
        if (whose == Whose.Superior)
            a.Asked = false;   // in its superior's hands again
        else
            Gather(ctx, g);
        a.Whose = whose;
        a.Conn = whose == Whose.Player && (code == AiCommand.Conn || a.Conn && Chain.IsHelmOrder(code));   // as a captain's
        (a.Turning, a.Away, a.DeployMs) = (false, false, 0);   // a new order ends a turn under way, a turn-away, a deployment

        if (code == AiCommand.TurnTogether)
        {
            StartTurn(ctx, g, a, c.X, c.Y != 0, keep: false);
            AskHeld(g);
            return;
        }

        int lead = ctx.Ships.SlotOf(groups.GuideShip(g));
        if (code == AiCommand.Deploy)
        {
            if (lead >= 0)
                DeployOrdered(ctx, g, a, c.X, lead);
            AskHeld(g);
            return;
        }
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
            case AiCommand.Hold or AiCommand.Conn:   // the conn taken: on the course and speed ordered now
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
            case AiCommand.Screen:
                (a.Order, a.Target, a.DistanceM, a.AbeamM) = (CaptainOrder.Screen, (int)c.A, c.X > 0 ? c.X : ctx.Settings.ScreenM, 0);
                (a.ScreenOn, a.ScreenFight, a.ScreenFightSide, a.ScreenSide, a.ScreenAway, a.AttackOrdered) = (0, 0, 0, 0, false, false);
                a.ScreenSplitDeg = c.Y > 0 ? c.Y : c.Y < 0 ? 0 : ctx.Settings.ScreenSplitDeg;
                if (a.ScreenSplitDeg > 0 && !g.IsForce && g.Members.Count >= 2)
                    ctx.Issue(GroupSystem.Divide(g.GroupId, (g.Members.Count + 1) / 2));   // two divisions, each a unit (O1b)
                break;
            case AiCommand.Attack:
                (a.Order, a.Target, a.DistanceM, a.AbeamM) = (CaptainOrder.Fight, Unit.Ship((int)c.A).Id, 0, 0);
                (a.AttackOrdered, a.Attack, a.AttackTarget) = (true, AttackPhase.None, 0);   // from a fresh start
                break;
            case AiCommand.Follow or AiCommand.Station or AiCommand.Run or AiCommand.Shadow or AiCommand.Fight:
                a.AttackOrdered = false;
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
        a.Watch = a.Order == CaptainOrder.Route && whose != Whose.Superior && detached == Detachment.Nudged;
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
                // turning together: every ship has her course; no stations, no new guide's orders until it's done (a
                // flotilla firing as it turns)
                FireTogether(ctx, g, a);
                if (a.Fire == FireOrder.Engage)
                    HandFire(ctx, g, a, fresh: false);
                Limit(ctx, g, a);
                Clear(ctx, g, a);
                continue;
            }
            if (a.HandedTo != to.Id)
                HandOver(ctx, a, to);
            bool deploying = DeployStep(ctx, g, a, lead);
            if (deploying && a.DeployMs == ctx.NowMs)
                continue;   // just begun: the line is given its stations, and its guide its course, next think
            if (!deploying)
                TakeUpStanding(ctx, g, a, lead);
            if (TurnAway(ctx, g, a, lead))
                continue;   // turning together: no stations until it's round
            ScreenStep(ctx, g, a);
            if (AttackStep(ctx, g, a, lead))
                continue;   // the flotilla turning together to fire
            if (a.Fire == FireOrder.Engage)
                HandFire(ctx, g, a, fresh: false);
            Limit(ctx, g, a);
            Clear(ctx, g, a);
            var members = g.Line;
            for (int i = 1; i < members.Count; i++)
            {
                if (ScreenMember(ctx, g, a, i))
                    continue;   // its place in a split screen
                if (!g.IsForce)
                {
                    var (target, astern, abeam) = GroupSystem.StationOf(g, i);
                    if (!captains.Holds(members[i], CaptainOrder.Station, target, astern, abeam))
                        ctx.Issue(ctx.Orders.Station(Unit.Ship(members[i]), Unit.Ship(target), astern, abeam, OrderSource.Superior));
                }
                else
                {
                    var (target, astern, abeam) = InDeployment(g) ? DeployStation(g, i) : groups.StationOfGroup(g, i);
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
            if (admirals[i].Whose == Whose.Superior && groups.Get(admirals[i].GroupId) is { } g && !groups.InForceLine(g))
                Release(ctx, g, admirals[i]);
    }

    /// <summary>Whether a group's admiral already holds his superior's order to keep this station on a ship.</summary>
    bool Holds(int groupId, int ship, double astern, double abeam) =>
        Find(groupId) is int i and >= 0 && admirals[i] is { Whose: Whose.Superior, Order: CaptainOrder.Station } a &&
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

    /// <summary>A group's orders changed: each member the player holds is asked back (not one on its task).</summary>
    void AskHeld(GroupSystem.Group g)
    {
        for (int i = 0; i < g.Members.Count; i++)
            if (g.DetachmentAt(i) is Detachment.Nudged or Detachment.Pinned)
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
        if (a.Order is not (CaptainOrder.Course or CaptainOrder.Route) && lead >= 0 && to.Id != 0)
        {
            (a.Order, a.CourseDeg, a.RudderDeg, a.RouteCount, a.Loop) = (CaptainOrder.Course, double.NaN, 0, 0, false);
            a.AskedKn = ctx.Ships[lead].SpeedOrderKn;
            ctx.Issue(ctx.Orders.Hold(to, OrderSource.Superior));
        }
        (a.LimitAbove, a.ClearAbove, a.Whose, a.Asked) = (0, 0, Whose.Own, false);
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
            case CaptainOrder.Screen:
                if (a.ScreenFight != 0 && (a.ScreenFightSide == 0 || a.ScreenFightSide == a.ScreenSide))
                    ctx.Issue(o.Fight(to, Unit.Ship(a.ScreenFight), 0, Up));
                else if (a.ScreenOn != 0)
                    ctx.Issue(o.Station(to, Unit.Ship(a.ScreenOn), a.ScreenAstern, a.ScreenAbeam, Up));
                else
                    ctx.Issue(o.Hold(to, Up));   // given its station by the next think
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
            h.Add((int)a.Whose);
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
            h.Add(a.Conn);
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
            if (a.DeployMs != 0 || a.Deployed)
            {
                h.Add(a.DeployMs);
                h.Add(a.Deployed);
            }
            if (a.Order == CaptainOrder.Screen)
            {
                h.Add(a.ScreenOn);
                h.Add(a.ScreenAstern);
                h.Add(a.ScreenAbeam);
                h.Add(a.ScreenFight);
                h.Add(a.ScreenThreatMs);
                h.Add(a.ScreenSplitDeg);
                h.Add(a.ScreenAxisDeg);
                h.Add(a.ScreenSide);
                h.Add(a.ScreenFightSide);
                h.Add(a.ScreenAway);
            }
            if (a.Attack != AttackPhase.None || a.AttackOrdered)
            {
                h.Add((int)a.Attack);
                h.Add(a.AttackOrdered);
                h.Add(a.AttackTarget);
                h.Add(a.AttackShips);
                h.Add(a.AttackSide);
                h.Add(a.AttackTubes);
                h.Add(a.AttackMs);
            }
        }
    }

    public void Fill(Snapshot s)
    {
        var v = s.Groups;
        for (int k = 0; k < v.Count; k++)
        {
            v.Asked[k] = IsAsked(v.Id[k]);
            v.Conn[k] = Find(v.Id[k]) is int i and >= 0 && admirals[i].Conn;
        }
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
