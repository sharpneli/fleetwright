using Fleetwright.Sim.Ai.Admirals;
using Fleetwright.Sim.Ai.Captains;
using Fleetwright.Sim.Ai.Knowledge;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai.Tasks;

/// <summary>What a unit's been tasked with (docs/battle/command.md "Tasks: the vocabulary grows").</summary>
public enum TaskKind : byte
{
    None,
    GoTo,
    Patrol,
    Intercept,
    Join,
    Withdraw,
}

/// <summary>How a task stands, reported up (docs/battle/command.md "Reports and reasons").</summary>
public enum TaskState : byte
{
    None,

    /// <summary>Doing it.</summary>
    OnTask,

    /// <summary>Fighting (doctrine's Engage took it up): the task waits, and goes on once no enemy's been in sight for
    /// <see cref="SimSettings.TaskResumeS"/>.</summary>
    Engaged,

    /// <summary>Done (<see cref="TaskReason"/> says how): the unit holds what it was last given.</summary>
    Done,

    /// <summary>Can't be done (<see cref="TaskReason"/> says why): the unit holds what it was last given.</summary>
    Cant,
}

/// <summary>Why a task stands as it does.</summary>
public enum TaskReason : byte
{
    None,
    Arrived,
    InContact,
    Joined,
    Home,

    /// <summary>The contact it was after is gone from the picture.</summary>
    LostContact,

    /// <summary>The unit it was to join or screen is gone.</summary>
    Gone,

    /// <summary>There's nowhere to go (no home).</summary>
    Nowhere,
}

/// <summary>The task layer (docs/battle/command.md 5.1c), above the admirals: a unit's task (a kind of the one unit
/// order, given by the player or, from 5.1d, the side's commander) and its standing. Each task turns into orders the
/// chain already has (a route, a course, a run, a group's place in a force), given to the unit itself as its own
/// (<see cref="OrderSource.Task"/>), and says how it ends. A unit in a squadron's or a force's line is detached (pinned)
/// to do its task; Join puts it back. A fight doctrine takes up (Engage) interrupts a task: it waits, and goes on once no
/// enemy has been in sight of the unit for <see cref="SimSettings.TaskResumeS"/>. The player's own order to the unit ends
/// its task (a change of speed doesn't: it's the task's speed then). A task done, or that can't be, leaves its unit
/// holding what it was last given, its status the report up: 5.1d's commander gives it the next.
/// <para>Thinks every <see cref="SimSettings.ForcePeriodMs"/> while any tasked unit is on the fine tick, the coarse step
/// while any is coarse, else the rails period. Tasks are kept in unit order; nothing allocated once grown.</para></summary>
public sealed class TaskLayer(GroupSystem groups, SeenContacts seen, Vec3d?[] homes) : IAiLayer
{
    struct Task
    {
        public int Unit;                // Unit.Id
        public TaskKind Kind;
        public TaskState Status;
        public TaskReason Reason;
        public int Target;              // a contact's entity (Intercept), a unit (Join)
        public long Point, From;        // packed lat/lons: the goal; a patrol's other end
        public double Kn;               // the task's speed (0: as rung, or all it makes)
        public bool Issued;             // its orders given (again after a fight)
        public double CourseDeg;        // the course last given (Intercept, Join)
        public int RunFrom;             // Withdraw: the enemy it's running from (0: going home)
        public long StartMs, QuietMs;   // when it began; when the unit last had an enemy in sight
    }

    Task[] tasks = new Task[8];
    int count;

    internal AdmiralLayer? Admirals { get; set; }
    internal CaptainLayer? Captains { get; set; }

    public bool Active
    {
        get
        {
            for (int i = 0; i < count; i++)
                if (tasks[i].Status is TaskState.OnTask or TaskState.Engaged)
                    return true;
            return false;
        }
    }

    public int PeriodMs(in AiContext ctx)
    {
        int period = ctx.Settings.AiRailsPeriodMs;
        for (int i = 0; i < count; i++)
            if (tasks[i].Status is TaskState.OnTask or TaskState.Engaged && ctx.Ships.SlotOf(Lead(new Unit(tasks[i].Unit))) is int slot and >= 0)
            {
                var tier = ctx.Ships.TierOf(slot);
                if (tier == ClockTier.Fine)
                    return ctx.Settings.ForcePeriodMs;
                if (tier == ClockTier.Coarse)
                    period = Math.Min(period, ctx.Settings.CoarseStepMs);
            }
        return period;
    }

    /// <summary>A unit's task and how it stands; None if it has none.</summary>
    public (TaskKind Kind, TaskState Status, TaskReason Reason) Of(Unit u) =>
        Find(u.Id) is int i and >= 0 ? (tasks[i].Kind, tasks[i].Status, tasks[i].Reason) : (TaskKind.None, TaskState.None, TaskReason.None);

    /// <summary>Whether a unit's task keeps it out of fights doctrine would take up (withdrawing).</summary>
    public bool Avoids(Unit u) => Find(u.Id) is int i and >= 0 && tasks[i].Status == TaskState.OnTask && tasks[i].Kind == TaskKind.Withdraw;

    /// <summary>The player's own order to a unit: its task ends (a speed is the task's speed).</summary>
    internal void Overridden(Unit u, in Command c)
    {
        if (Find(u.Id) is not (int i and >= 0))
            return;
        if ((AiCommand)c.Code == AiCommand.Speed)
            tasks[i].Kn = c.X;
        else
            Remove(i);
    }

    /// <summary>A task for a unit (its command).</summary>
    public void Order(in AiContext ctx, in Command c)
    {
        var u = new Unit(c.Entity);
        if (Lead(u) == 0)
            return;
        var kind = (AiCommand)c.Code switch
        {
            AiCommand.GoTo => TaskKind.GoTo,
            AiCommand.Patrol => TaskKind.Patrol,
            AiCommand.Intercept => TaskKind.Intercept,
            AiCommand.Join => TaskKind.Join,
            AiCommand.Withdraw => TaskKind.Withdraw,
            _ => throw new ArgumentException($"not a task: {c.Code}"),
        };
        ref var t = ref Get(u.Id);
        t = new Task
        {
            Unit = u.Id, Kind = kind, Status = TaskState.OnTask, StartMs = ctx.NowMs, QuietMs = ctx.NowMs,
            Point = kind is TaskKind.GoTo or TaskKind.Patrol or TaskKind.Withdraw ? c.A : 0,
            Target = kind is TaskKind.Intercept or TaskKind.Join ? (int)c.A : 0,
            Kn = c.X,
        };
        if (kind == TaskKind.Patrol && ctx.Ships.SlotOf(Lead(u)) is int slot and >= 0)
        {
            var (lat, lon) = Earth.LatLon(ctx.Ships[slot].Position);
            t.From = Earth.PackLatLon(lat, lon);
        }
        OutOfLine(ctx, u);
        Step(ctx, ref t);
    }

    public void Think(in AiContext ctx)
    {
        for (int i = 0; i < count; i++)
        {
            if (Lead(new Unit(tasks[i].Unit)) is var lead && ctx.Ships.SlotOf(lead) < 0)
            {
                Remove(i--);   // the unit's gone
                continue;
            }
            if (tasks[i].Status is TaskState.OnTask or TaskState.Engaged)
                Step(ctx, ref tasks[i]);
        }
    }

    /// <summary>A task, each think: waiting while its unit fights, else its orders given (once, or as they change) and
    /// its end looked for.</summary>
    void Step(in AiContext ctx, ref Task t)
    {
        var s = ctx.Settings;
        var u = new Unit(t.Unit);
        int slot = ctx.Ships.SlotOf(Lead(u));
        if (slot < 0)
            return;
        var guide = ctx.Ships[slot];
        long now = ctx.NowMs;
        if (seen.Near(ctx.World, guide.Side, guide.Position, s.FightCompareM).Length > 0)
            t.QuietMs = now;

        // a fight doctrine took up (or the captain's own: falling out) interrupts it
        var order = u.IsGroup ? Admirals!.Of(u.GroupId).Order : Captains!.Of(u.Entity).Order;
        if (order is CaptainOrder.Fight or CaptainOrder.Home && !(t.Kind == TaskKind.Withdraw && t.RunFrom != 0))
        {
            if (order == CaptainOrder.Home)
            {
                (t.Status, t.Reason) = (TaskState.Cant, TaskReason.None);   // fallen out: the captain's now
                return;
            }
            if (now - t.QuietMs < (long)(s.TaskResumeS * 1000))
            {
                (t.Status, t.Issued) = (TaskState.Engaged, false);
                return;
            }
        }
        t.Status = TaskState.OnTask;
        var o = ctx.Orders;
        const OrderSource Own = OrderSource.Task;
        switch (t.Kind)
        {
            case TaskKind.GoTo:
            {
                var (lat, lon) = Earth.UnpackLatLon(t.Point);
                if (Earth.Distance(guide.Position, Earth.Position(lat, lon)) <= s.ArrivedM)
                {
                    End(ref t, TaskState.Done, TaskReason.Arrived);
                    break;
                }
                if (!t.Issued)
                {
                    ctx.Issue(o.ClearRoute(u, Own));
                    ctx.Issue(o.AddWaypoint(u, t.Point, Own));
                    if (t.Kn > 0)
                        ctx.Issue(o.Speed(u, t.Kn, Own));
                    t.Issued = true;
                }
                break;
            }
            case TaskKind.Patrol:
                if (!t.Issued)
                {
                    ctx.Issue(o.ClearRoute(u, Own));
                    ctx.Issue(o.AddWaypoint(u, t.Point, Own));
                    ctx.Issue(o.AddWaypoint(u, t.From, Own));
                    ctx.Issue(o.LoopRoute(u, true, Own));
                    t.Issued = true;
                }
                break;
            case TaskKind.Withdraw:
                Withdraw(ctx, ref t, u, guide);
                break;
            case TaskKind.Intercept:
            {
                var pic = ctx.World.Sightings.Of(guide.Side);
                int ti = pic?.IndexOf(t.Target) ?? -1;
                if (ti < 0)
                {
                    End(ref t, TaskState.Cant, TaskReason.LostContact);
                    break;
                }
                var (q, v, _) = pic!.EstimateAt(ti, now, s);
                if (pic[ti].InSight && (q - guide.Position).Length <= s.FightCompareM)
                {
                    End(ref t, TaskState.Done, TaskReason.InContact);   // doctrine's from here: fight, or not
                    break;
                }
                Steer(ctx, ref t, u, guide, q, v);
                break;
            }
            case TaskKind.Join:
                Join(ctx, ref t, u, guide);
                break;
        }
    }

    /// <summary>Withdraw: from the nearest enemy in sight while there is one, else to its point (or its side's home);
    /// done within <see cref="SimSettings.HomeRadiusM"/> of it, stopped.</summary>
    void Withdraw(in AiContext ctx, ref Task t, Unit u, ShipState guide)
    {
        var s = ctx.Settings;
        var o = ctx.Orders;
        int nearest = NearestEnemy(ctx, guide, s.FightCompareM);
        if (nearest != 0)
        {
            if (t.RunFrom != nearest)
            {
                t.RunFrom = nearest;
                t.Issued = false;
                ctx.Issue(o.Run(u, Unit.Ship(nearest), OrderSource.Task));
            }
            return;
        }
        t.RunFrom = 0;
        Vec3d goal;
        if (t.Point != 0)
        {
            var (lat, lon) = Earth.UnpackLatLon(t.Point);
            goal = Earth.Position(lat, lon);
        }
        else if (homes[guide.Side] is { } home)
            goal = home;
        else
        {
            End(ref t, TaskState.Cant, TaskReason.Nowhere);
            return;
        }
        if (Earth.Distance(guide.Position, goal) <= s.HomeRadiusM)
        {
            ctx.Issue(o.Speed(u, 0, OrderSource.Task));
            End(ref t, TaskState.Done, TaskReason.Home);
            return;
        }
        if (!t.Issued)
        {
            var (lat, lon) = Earth.LatLon(goal);
            ctx.Issue(o.ClearRoute(u, OrderSource.Task));
            ctx.Issue(o.AddWaypoint(u, lat, lon, OrderSource.Task));
            ctx.Issue(o.Speed(u, t.Kn > 0 ? t.Kn : guide.Handling.Class.Motion.MaxSpeedKn, OrderSource.Task));
            t.Issued = true;
        }
    }

    /// <summary>Join: close on the unit (where it'll be) at all the unit makes; within <see cref="SimSettings.JoinM"/> of its
    /// nearest end, into it: a ship into its squadron, a group into its force (a squadron alone forms one, in line
    /// ahead), one detached from it back into its line.</summary>
    void Join(in AiContext ctx, ref Task t, Unit u, ShipState guide)
    {
        var s = ctx.Settings;
        var with = new Unit(t.Target);
        int theirs = Lead(with);
        if (ctx.Ships.SlotOf(theirs) is not (int ts and >= 0) || theirs == guide.Entity || ctx.Ships[ts].Side != guide.Side)
        {
            End(ref t, TaskState.Cant, TaskReason.Gone);
            return;
        }
        var them = ctx.Ships[ts];
        var wg = with.IsGroup ? groups.Get(with.GroupId) : groups.Of(with.Entity);
        int last = wg != null ? groups.LastShip(wg) : 0;
        double near = Earth.Distance(guide.Position, them.Position);
        if (ctx.Ships.SlotOf(last) is int ls and >= 0)
            near = Math.Min(near, Earth.Distance(guide.Position, ctx.Ships[ls].Position));
        if (near > s.JoinM)
        {
            Steer(ctx, ref t, u, guide, them.Position, them.Forward * them.Speed);
            return;
        }
        var o = ctx.Orders;
        if (u.IsShip)
        {
            if (wg == null || wg.IsForce)
            {
                End(ref t, TaskState.Cant, TaskReason.Gone);   // a ship joins a squadron
                return;
            }
            if (groups.Of(u.Entity) == wg)
                ctx.Issue(o.Resume(u, OrderSource.Task));
            else
                ctx.Issue(GroupSystem.Add(wg.GroupId, u.Entity));
        }
        else if (groups.Get(u.GroupId) is { } mine)
        {
            var force = wg == null ? null : wg.IsForce ? wg : wg.Parent != 0 ? groups.Get(wg.Parent) : null;
            if (force != null && mine.Parent == force.GroupId)
                ctx.Issue(o.Resume(u, OrderSource.Task));
            else if (force != null)
                ctx.Issue(GroupSystem.Enter(force.GroupId, mine.GroupId));
            else if (wg != null)
            {
                // a squadron alone: a force of it, in one line, this one astern
                ctx.Issue(GroupSystem.Form(wg.GroupId, Formation.Column, wg.SpacingM));
                ctx.Issue(GroupSystem.EnterAlongside(mine.GroupId, wg.GroupId));
            }
            else
            {
                End(ref t, TaskState.Cant, TaskReason.Gone);
                return;
            }
        }
        End(ref t, TaskState.Done, TaskReason.Joined);
    }

    /// <summary>The unit on the course that meets a point moving at a velocity, at the task's speed (or all it makes),
    /// given again only when it moves past the captains' deadband.</summary>
    static void Steer(in AiContext ctx, ref Task t, Unit u, ShipState guide, Vec3d at, Vec3d velocity)
    {
        var s = ctx.Settings;
        double kn = t.Kn > 0 ? t.Kn : guide.Handling.Class.Motion.MaxSpeedKn;
        double course = TorpedoAim.Intercept(guide.Position, at, velocity, kn * Handling.MsPerKnot, out double c, out _) ? c : Earth.Bearing(guide.Position, at);
        var o = ctx.Orders;
        if (!t.Issued || Math.Abs(Wrap180(course - t.CourseDeg)) > s.CaptainCourseDeadbandDeg)
        {
            ctx.Issue(o.Course(u, course, 0, OrderSource.Task));
            t.CourseDeg = course;
        }
        if (!t.Issued)
            ctx.Issue(o.Speed(u, kn, OrderSource.Task));
        t.Issued = true;
    }

    /// <summary>A unit in a squadron's or a force's line comes out of it (pinned) for its task.</summary>
    void OutOfLine(in AiContext ctx, Unit u)
    {
        bool inLine = u.IsShip ? groups.InLine(u.Entity) : groups.Get(u.GroupId) is { } g && groups.InForceLine(g);
        if (inLine)
            ctx.Issue(ctx.Orders.Detach(u, OrderSource.Task));
    }

    static void End(ref Task t, TaskState status, TaskReason reason) => (t.Status, t.Reason) = (status, reason);

    /// <summary>The nearest enemy the unit's navy holds in sight within a radius of its guide, 0 if none.</summary>
    int NearestEnemy(in AiContext ctx, ShipState guide, double radiusM)
    {
        var pic = ctx.World.Sightings.Of(guide.Side);
        int best = 0;
        double bestD = double.MaxValue;
        foreach (int i in seen.Near(ctx.World, guide.Side, guide.Position, radiusM))
        {
            double d = (seen.Position(guide.Side, i) - guide.Position).LengthSquared;
            if (d < bestD)
                (best, bestD) = (pic![i].Entity, d);
        }
        return best;
    }

    /// <summary>The ship a unit stands for on the sea: itself, or its group's guide ship (0: none).</summary>
    int Lead(Unit u) => u.IsShip ? u.Entity : groups.Get(u.GroupId) is { } g ? groups.GuideShip(g) : 0;

    static double Wrap180(double d) => d - 360 * Math.Round(d / 360);

    public void Hash(ref StateHash h)
    {
        if (count == 0)
            return;   // a world with no tasks hashes as before them
        h.Add(count);
        for (int i = 0; i < count; i++)
        {
            ref readonly var t = ref tasks[i];
            h.Add(t.Unit);
            h.Add((int)t.Kind);
            h.Add((int)t.Status);
            h.Add((int)t.Reason);
            h.Add(t.Target);
            h.Add(t.Point);
            h.Add(t.From);
            h.Add(t.Kn);
            h.Add(t.Issued);
            h.Add(t.CourseDeg);
            h.Add(t.RunFrom);
            h.Add(t.StartMs);
            h.Add(t.QuietMs);
        }
    }

    public void Fill(Snapshot s)
    {
        var gv = s.Groups;
        for (int k = 0; k < gv.Count; k++)
            (gv.Task[k], gv.TaskStatus[k]) = Find(Unit.Group(gv.Id[k]).Id) is int i and >= 0 ? ((byte)tasks[i].Kind, (byte)tasks[i].Status) : ((byte)0, (byte)0);
        var v = s.Ships;
        for (int k = 0; k < v.Count; k++)
            (v.Task[k], v.TaskStatus[k]) = Find(v.Entity[k]) is int i and >= 0 ? ((byte)tasks[i].Kind, (byte)tasks[i].Status) : ((byte)0, (byte)0);
    }

    ref Task Get(int unit)
    {
        int i = Find(unit);
        if (i >= 0)
            return ref tasks[i];
        if (count == tasks.Length)
            Array.Resize(ref tasks, tasks.Length * 2);
        i = count;
        while (i > 0 && tasks[i - 1].Unit > unit)
        {
            tasks[i] = tasks[i - 1];
            i--;
        }
        tasks[i] = new Task { Unit = unit };
        count++;
        return ref tasks[i];
    }

    int Find(int unit)
    {
        int lo = 0, hi = count - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            int id = tasks[mid].Unit;
            if (id == unit)
                return mid;
            if (id < unit)
                lo = mid + 1;
            else
                hi = mid - 1;
        }
        return -1;
    }

    void Remove(int i)
    {
        Array.Copy(tasks, i + 1, tasks, i, count - i - 1);
        tasks[--count] = default;
    }
}
