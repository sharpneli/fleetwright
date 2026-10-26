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
    Shadow,
    Screen,
    Scout,
    Search,
    Cover,

    /// <summary>A flotilla's torpedo attack (docs/battle/command.md 5.1b), as a task: done once its torpedoes are spent.</summary>
    Attack,
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

    /// <summary>In touch with an enemy too strong to fight: shadowing him (a scout), its task waiting.</summary>
    InContact,
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

    /// <summary>A search's pattern steamed to its end.</summary>
    Searched,

    /// <summary>An attack's torpedoes spent.</summary>
    Fired,

    /// <summary>An attack beaten off (doctrine's AttackBreakOffShare of its boats gone).</summary>
    BeatenOff,
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
        public double DistanceM, OffBowDeg;   // Scout, Cover, Screen, Shadow: how far off; a scout's bearing off the bow
        public int ShadowOf;            // Scout: the enemy it's shadowing (0: scouting)
        public int StationOn;           // Scout, Cover: the ship its station was given on
        public double StationAstern, StationAbeam;
        public long LastWaypoint;       // Search: the route's end
        public long StartMs, QuietMs;   // when it began; when the unit last had an enemy in sight
        public bool Hand;               // given by the player's hand: carried out as the AI's, but literally
    }

    Task[] tasks = new Task[8];
    int count;

    // the tasks still to come (each unit's plan, docs/battle/command.md 5.1d), their commands in the order given; a unit's
    // next starts when its task ends, done or not
    Command[] queue = new Command[8];
    int queued;

    internal AdmiralLayer? Admirals { get; set; }
    internal CaptainLayer? Captains { get; set; }
    internal Doctrine? Doctrine { get; set; }

    /// <summary>Who can fight whom (docs/battle/command.md 5.1g): a scout's judgement.</summary>
    internal Knowledge.MatchupJudge Judge { get; set; } = null!;

    static bool Live(TaskState s) => s is TaskState.OnTask or TaskState.Engaged or TaskState.InContact;

    public bool Active
    {
        get
        {
            if (queued > 0)
                return true;
            for (int i = 0; i < count; i++)
                if (Live(tasks[i].Status))
                    return true;
            return false;
        }
    }

    public int PeriodMs(in AiContext ctx)
    {
        int period = ctx.Settings.AiRailsPeriodMs;
        for (int i = 0; i < count; i++)
            if (Live(tasks[i].Status) && ctx.Ships.SlotOf(Lead(new Unit(tasks[i].Unit))) is int slot and >= 0)
            {
                var tier = ctx.Ships.TierOf(slot);
                if (tier == ClockTier.Fine)
                    return ctx.Settings.ForcePeriodMs;
                if (tier == ClockTier.Coarse)
                    period = Math.Min(period, ctx.Settings.CoarseStepMs);
            }
        return period;
    }

    /// <summary>How many units have a task, and the i-th (in unit order): for reports (the play bench's timeline).</summary>
    public int Count => count;

    public (Unit Unit, TaskKind Kind, TaskState State, TaskReason Reason) At(int i) =>
        (new Unit(tasks[i].Unit), tasks[i].Kind, tasks[i].Status, tasks[i].Reason);

    /// <summary>A unit's task and how it stands; None if it has none.</summary>
    public (TaskKind Kind, TaskState Status, TaskReason Reason) Of(Unit u) =>
        Find(u.Id) is int i and >= 0 ? (tasks[i].Kind, tasks[i].Status, tasks[i].Reason) : (TaskKind.None, TaskState.None, TaskReason.None);

    /// <summary>How many tasks a unit has still to come after the one it's on.</summary>
    public int QueuedOf(Unit u)
    {
        int n = 0;
        for (int q = 0; q < queued; q++)
            if (queue[q].Entity == u.Id)
                n++;
        return n;
    }

    /// <summary>Whether a unit has had a task and is through with it: it ended (done, or it couldn't be), nothing after
    /// it. A unit that's never had one is on the orders it was given, not idle.</summary>
    public bool Idle(Unit u) => Find(u.Id) is int i and >= 0 && !Live(tasks[i].Status) && QueuedOf(u) == 0;

    /// <summary>Whether a unit is on a task (doing it, fighting with it waiting, or in contact).</summary>
    public bool OnTask(Unit u) => Find(u.Id) is int i and >= 0 && Live(tasks[i].Status);

    /// <summary>Whether a unit's task (live) was given by the player's hand: its admiral carries it out as given, never
    /// reorganising the unit for it.</summary>
    public bool ByHand(Unit u) => Find(u.Id) is int i and >= 0 && Live(tasks[i].Status) && tasks[i].Hand;

    /// <summary>When a unit's task began, ms of world time (0: none): who gave it can tell it's still theirs.</summary>
    public long StartOf(Unit u) => Find(u.Id) is int i and >= 0 ? tasks[i].StartMs : 0;

    /// <summary>A unit's task's target: the unit or contact it's about (0: a point, or none).</summary>
    public int TargetOf(Unit u) => Find(u.Id) is int i and >= 0 ? tasks[i].Target : 0;

    /// <summary>Whether a unit's task keeps it out of fights doctrine would take up: withdrawing, shadowing, a scout
    /// shadowing what it found.</summary>
    public bool Avoids(Unit u) => Find(u.Id) is int i and >= 0 && tasks[i] is
    {
        Status: TaskState.OnTask or TaskState.InContact,
        Kind: TaskKind.Withdraw or TaskKind.Shadow or TaskKind.Scout,
    } t && (t.Kind != TaskKind.Scout || t.ShadowOf != 0);

    /// <summary>Whether a command is a task for this layer: the task kinds, and Shadow and Screen when the player (or a
    /// scenario) or the side's commander gives them; the same from a task, or a superior handing them down, are the plain
    /// orders.</summary>
    public static bool IsTask(AiCommand code, OrderSource source) =>
        code is AiCommand.GoTo or AiCommand.Patrol or AiCommand.Intercept or AiCommand.Join or AiCommand.Withdraw or AiCommand.Scout
            or AiCommand.Search or AiCommand.Cover ||
        code is AiCommand.Shadow or AiCommand.Screen or AiCommand.Attack && source is OrderSource.Player or OrderSource.Commander;

    /// <summary>The task a command gives.</summary>
    public static TaskKind KindOf(AiCommand code) => code switch
    {
        AiCommand.GoTo => TaskKind.GoTo,
        AiCommand.Patrol => TaskKind.Patrol,
        AiCommand.Intercept => TaskKind.Intercept,
        AiCommand.Join => TaskKind.Join,
        AiCommand.Withdraw => TaskKind.Withdraw,
        AiCommand.Shadow => TaskKind.Shadow,
        AiCommand.Screen => TaskKind.Screen,
        AiCommand.Scout => TaskKind.Scout,
        AiCommand.Search => TaskKind.Search,
        AiCommand.Cover => TaskKind.Cover,
        AiCommand.Attack => TaskKind.Attack,
        _ => throw new ArgumentException($"not a task: {code}"),
    };

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

    /// <summary>A task for a unit (its command): at once, in place of its task and its plan; or, given as
    /// <see cref="UnitOrders.Then"/>, after the tasks it already has.</summary>
    public void Order(in AiContext ctx, in Command c)
    {
        var u = new Unit(c.Entity);
        if ((c.B & UnitOrders.Queued) != 0 && (Find(u.Id) is int i and >= 0 && Live(tasks[i].Status) || QueuedOf(u) > 0))
        {
            if (queued == queue.Length)
                Array.Resize(ref queue, queue.Length * 2);
            queue[queued++] = c with { B = c.B & ~UnitOrders.Queued };
            return;
        }
        Unqueue(u);
        Begin(ctx, c with { B = c.B & ~UnitOrders.Queued });
    }

    /// <summary>A unit's next task begun, its command.</summary>
    void Begin(in AiContext ctx, in Command c)
    {
        var u = new Unit(c.Entity);
        if (Lead(u) == 0)
            return;
        var kind = KindOf((AiCommand)c.Code);
        var s = ctx.Settings;
        ref var t = ref Get(u.Id);
        t = new Task
        {
            Unit = u.Id, Kind = kind, Status = TaskState.OnTask, StartMs = ctx.NowMs, QuietMs = ctx.NowMs, Hand = OrderFrom.Of(c).Hand,
            Point = kind is TaskKind.GoTo or TaskKind.Patrol or TaskKind.Withdraw or TaskKind.Search ? c.A : 0,
            Target = kind is TaskKind.Intercept or TaskKind.Join or TaskKind.Shadow or TaskKind.Screen or TaskKind.Scout or TaskKind.Cover or TaskKind.Attack ? (int)c.A : 0,
            Kn = kind is TaskKind.GoTo or TaskKind.Intercept ? c.X : 0,
            DistanceM = kind switch
            {
                TaskKind.Scout => c.X > 0 ? c.X : s.ScoutM,
                TaskKind.Cover => c.X > 0 ? c.X : s.CoverM,
                TaskKind.Search => c.X > 0 ? c.X : s.SearchLegM,
                TaskKind.Shadow or TaskKind.Screen => c.X,
                _ => 0,
            },
            OffBowDeg = kind == TaskKind.Scout ? c.Y : 0,
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
            bool live = Live(tasks[i].Status);
            if (live)
                Step(ctx, ref tasks[i]);
            // a task the player gave by hand ended, the last of its plan: what a run waits for (docs/map-ui/input.md T1)
            if (live && !Live(tasks[i].Status) && tasks[i].Hand && Next(tasks[i].Unit) < 0
                && ctx.Ships.SlotOf(lead) is int ls and >= 0 && ctx.Ships[ls].Side == ctx.World.PlayerSide)
                ctx.World.Matters();
            if (queued > 0 && !Live(tasks[i].Status) && Next(tasks[i].Unit) is int q and >= 0)
            {
                var c = queue[q];
                Array.Copy(queue, q + 1, queue, q, queued - q - 1);
                queue[--queued] = default;
                Begin(ctx, c);   // the plan goes on, whether the last was done or couldn't be
            }
        }
    }

    int Next(int unit)
    {
        for (int q = 0; q < queued; q++)
            if (queue[q].Entity == unit)
                return q;
        return -1;
    }

    /// <summary>A unit's plan dropped.</summary>
    void Unqueue(Unit u)
    {
        int k = 0;
        for (int q = 0; q < queued; q++)
            if (queue[q].Entity != u.Id)
                queue[k++] = queue[q];
        Array.Clear(queue, k, queued - k);
        queued = k;
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

        // a scout judges what it meets before any fight doctrine took up stands: too strong for it, it shadows
        if (t.Kind == TaskKind.Scout && t.ShadowOf == 0 && Outmatched(ctx, ref t, u, guide))
            return;

        // an attack is a fight: it isn't interrupted by one
        if (t.Kind == TaskKind.Attack)
        {
            Attack(ctx, ref t, u, guide, slot);
            return;
        }

        // a force deploying into its line of battle: the task waits for the fight
        if (u.IsGroup && Admirals!.Deploying(u.GroupId))
        {
            (t.Status, t.Issued, t.QuietMs) = (TaskState.Engaged, false, now);
            return;
        }

        // a fight doctrine took up (or the captain's own: falling out) interrupts it
        var order = u.IsGroup ? Admirals!.Of(u.GroupId).Order : Captains!.Of(u.Entity).Order;
        if (order == CaptainOrder.Home)
        {
            (t.Status, t.Reason) = (TaskState.Cant, TaskReason.None);   // fallen out: the captain's now
            return;
        }
        if (order == CaptainOrder.Fight && t.Kind != TaskKind.Withdraw && now - t.QuietMs < (long)(s.TaskResumeS * 1000))
        {
            (t.Status, t.Issued) = (TaskState.Engaged, false);   // a withdrawal never waits on a fight: it breaks it off
            return;
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
                if (order == CaptainOrder.Fight)
                    (t.RunFrom, t.Issued) = (0, false);   // a fight took it up after all: away again
                Withdraw(ctx, ref t, u, guide);
                break;
            case TaskKind.Intercept:
            {
                var pic = ctx.World.Sightings.Of(guide.Side);
                int ti = pic?.IndexOf(t.Target) ?? -1;
                if (ti < 0 || !Fresh(ctx, pic![ti]))
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
            case TaskKind.Shadow:
                if (ctx.World.Sightings.Of(guide.Side)?.IndexOf(new Unit(t.Target).ShipOf(groups)) is not (int and >= 0))
                {
                    End(ref t, TaskState.Cant, TaskReason.LostContact);
                    break;
                }
                if (!t.Issued)
                {
                    ctx.Issue(o.Shadow(u, new Unit(t.Target), t.DistanceM, Own));
                    t.Issued = true;
                }
                break;
            case TaskKind.Screen:
                if (ctx.Ships.SlotOf(Lead(new Unit(t.Target))) < 0)
                {
                    End(ref t, TaskState.Cant, TaskReason.Gone);
                    break;
                }
                if (!t.Issued)
                {
                    ctx.Issue(o.Screen(u, new Unit(t.Target), t.DistanceM, Own));
                    t.Issued = true;
                }
                break;
            case TaskKind.Scout:
                Scout(ctx, ref t, u, guide);
                break;
            case TaskKind.Search:
                Search(ctx, ref t, u, guide);
                break;
            case TaskKind.Cover:
                Cover(ctx, ref t, u, guide);
                break;
        }
    }

    /// <summary>Whether a contact is fresh: in sight, or seen within <see cref="SimSettings.ContactFreshMs"/>. One older is
    /// lost: an intercept gives it up, and a commander chooses no target from it.</summary>
    public static bool Fresh(in AiContext ctx, in Contact c) => c.InSight || ctx.NowMs - c.SeenMs <= ctx.Settings.ContactFreshMs;

    /// <summary>Attack: the flotilla's attack (<see cref="AiCommand.Attack"/>) on its target, or the biggest in sight; done
    /// once no boat has a torpedo left, or no enemy has been in sight of it for <see cref="SimSettings.TaskResumeS"/>;
    /// can't be once it's beaten off (its admiral runs).</summary>
    void Attack(in AiContext ctx, ref Task t, Unit u, ShipState guide, int slot)
    {
        if (!t.Issued)
        {
            (t.Status, t.Issued) = (TaskState.OnTask, true);
            ctx.Issue(ctx.Orders.Attack(u, t.Target, OrderSource.Task));
            return;
        }
        if (u.IsGroup && groups.Get(u.GroupId) is { } g)
        {
            var (over, beatenOff) = Admirals!.AttackOver(ctx, g);
            if (over)
                End(ref t, beatenOff ? TaskState.Cant : TaskState.Done, beatenOff ? TaskReason.BeatenOff : TaskReason.Fired);
        }
        else if (!CaptainLayer.TorpedoesLeft(ctx, guide, slot))
            End(ref t, TaskState.Done, TaskReason.Fired);
        if (Live(t.Status) && ctx.NowMs - t.QuietMs > (long)(ctx.Settings.TaskResumeS * 1000))
            End(ref t, TaskState.Done, TaskReason.LostContact);
    }

    /// <summary>Scout: a station <see cref="Task.DistanceM"/> off the unit's guide, <see cref="Task.OffBowDeg"/> off its
    /// bow. An enemy in sight the scout is no match for (by the matchup, under doctrine's bar) is shadowed (the task in contact, and no fight taken up); one it can take, doctrine fights. A shadowed enemy
    /// lost, the scout goes back to its station.</summary>
    void Scout(in AiContext ctx, ref Task t, Unit u, ShipState guide)
    {
        var s = ctx.Settings;
        var o = ctx.Orders;
        if (t.ShadowOf != 0)
        {
            if (ctx.World.Sightings.Of(guide.Side)?.IndexOf(t.ShadowOf) is int and >= 0)
            {
                t.Status = TaskState.InContact;
                return;
            }
            (t.ShadowOf, t.Issued, t.StationOn) = (0, false, 0);   // lost him: back to scouting
        }
        int on = Lead(new Unit(t.Target));
        if (ctx.Ships.SlotOf(on) < 0 || on == guide.Entity)
        {
            End(ref t, TaskState.Cant, TaskReason.Gone);
            return;
        }
        double off = t.OffBowDeg * Math.PI / 180;
        Station(ctx, ref t, u, on, -t.DistanceM * Math.Cos(off), t.DistanceM * Math.Sin(off));
    }

    /// <summary>Whether a scout meets an enemy in sight it's no match for (its matchup against them under doctrine's bar,
    /// docs/battle/command.md 5.1g): then it shadows the nearest, taking up no fight (and leaving one doctrine took up).</summary>
    bool Outmatched(in AiContext ctx, ref Task t, Unit u, ShipState guide)
    {
        var s = ctx.Settings;
        int enemy = NearestEnemy(ctx, guide, s.FightCompareM);
        if (enemy == 0)
            return false;
        if (!Judge.Near(ctx, guide, out var m) || m.Confidence >= Judge.Bars(guide.Entity, guide.Side).Accept)
            return false;
        (t.ShadowOf, t.Status, t.Reason) = (enemy, TaskState.InContact, TaskReason.InContact);
        ctx.Issue(ctx.Orders.Shadow(u, Unit.Ship(enemy), 0, OrderSource.Task));
        return true;
    }

    /// <summary>Cover: a station <see cref="Task.DistanceM"/> off the covered unit's guide, toward the nearest enemy its
    /// navy holds anywhere (else on the side it's on now), given again when that moves it more than twice
    /// <see cref="SimSettings.ScreenMoveM"/>.</summary>
    void Cover(in AiContext ctx, ref Task t, Unit u, ShipState guide)
    {
        int on = Lead(new Unit(t.Target));
        if (ctx.Ships.SlotOf(on) is not (int slot and >= 0) || on == guide.Entity)
        {
            End(ref t, TaskState.Cant, TaskReason.Gone);
            return;
        }
        var them = ctx.Ships[slot];
        var threat = NearestHeld(ctx, them);
        double bearing = threat is { } q ? Earth.Bearing(them.Position, q) : Earth.Bearing(them.Position, guide.Position);
        double rel = (bearing - them.HeadingDeg) * Math.PI / 180;
        Station(ctx, ref t, u, on, -t.DistanceM * Math.Cos(rel), t.DistanceM * Math.Sin(rel), 2 * ctx.Settings.ScreenMoveM);
    }

    /// <summary>A station on a ship, given when it's new, after a fight or a shadow, or moved past a margin.</summary>
    static void Station(in AiContext ctx, ref Task t, Unit u, int on, double astern, double abeam, double margin = 0)
    {
        double moved = Math.Sqrt((astern - t.StationAstern) * (astern - t.StationAstern) + (abeam - t.StationAbeam) * (abeam - t.StationAbeam));
        if (t.Issued && t.StationOn == on && moved <= margin)
            return;
        (t.StationOn, t.StationAstern, t.StationAbeam, t.Issued) = (on, astern, abeam, true);
        ctx.Issue(ctx.Orders.Station(u, Unit.Ship(on), astern, abeam, OrderSource.Task));
    }

    /// <summary>Search: a group spread abreast <see cref="SimSettings.SearchSpacingShare"/> of the visibility apart (in
    /// sight of each other, a scouting line), to the point and then a ladder about it (legs <see cref="Task.DistanceM"/>
    /// long, on the course it came in on, its tracks a line's width apart); done on an enemy in sight, or at its end.</summary>
    void Search(in AiContext ctx, ref Task t, Unit u, ShipState guide)
    {
        var s = ctx.Settings;
        var o = ctx.Orders;
        if (NearestEnemy(ctx, guide, s.FightCompareM) != 0)
        {
            End(ref t, TaskState.Done, TaskReason.InContact);
            return;
        }
        if (t.Issued)
        {
            var (la, lo) = Earth.UnpackLatLon(t.LastWaypoint);
            if (guide.Helm != Helm.Route && Earth.Distance(guide.Position, Earth.Position(la, lo)) <= 2 * s.ArrivedM)
                End(ref t, TaskState.Done, TaskReason.Searched);
            return;
        }
        double spacing = s.SearchSpacingShare * ctx.World.Weather.At(guide.Position, ctx.World.Time).VisibilityM;
        int ships = 1;
        if (u.IsGroup && groups.Get(u.GroupId) is { IsForce: false } g)
        {
            ships = Math.Max(1, g.Line.Count);
            ctx.Issue(o.Form(u, Formation.LineAbreast, spacing, 0, OrderSource.Task));
        }
        var (lat, lon) = Earth.UnpackLatLon(t.Point);
        var datum = Earth.Position(lat, lon);
        double axis = Earth.Bearing(guide.Position, datum);
        Span<Vec3d> legs = stackalloc Vec3d[8];
        int n = SearchPatterns.Waypoints(SearchPattern.Ladder, datum, axis, t.DistanceM, ships * spacing, 4, starboard: true, legs);
        ctx.Issue(o.ClearRoute(u, OrderSource.Task));
        ctx.Issue(o.AddWaypoint(u, t.Point, OrderSource.Task));
        for (int i = 0; i < n; i++)
        {
            var (wla, wlo) = Earth.LatLon(legs[i]);
            ctx.Issue(o.AddWaypoint(u, wla, wlo, OrderSource.Task));
            t.LastWaypoint = Earth.PackLatLon(wla, wlo);
        }
        t.Issued = true;
    }

    /// <summary>Where a navy holds the nearest enemy to a ship, in sight or not (the picture's estimate); null if none.</summary>
    static Vec3d? NearestHeld(in AiContext ctx, ShipState from)
    {
        var pic = ctx.World.Sightings.Of(from.Side);
        if (pic == null)
            return null;
        Vec3d? best = null;
        double bestD = double.MaxValue;
        for (int i = 0; i < pic.Count; i++)
        {
            var q = pic.EstimateAt(i, ctx.NowMs, ctx.Settings).Position;
            double d = (q - from.Position).LengthSquared;
            if (d < bestD)
                (best, bestD) = (q, d);
        }
        return best;
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
        if (theirs == guide.Entity && u.IsGroup && with.IsGroup && groups.Get(u.GroupId) is { } guiding && guiding.Parent == with.GroupId)
        {
            ctx.Issue(ctx.Orders.Resume(u, OrderSource.Task));   // the force's own guide, back in its line
            End(ref t, TaskState.Done, TaskReason.Joined);
            return;
        }
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
        for (int q = 0; q < queued; q++)
        {
            ref readonly var c = ref queue[q];
            h.Add(c.Code);
            h.Add(c.Entity);
            h.Add(c.A);
            h.Add(c.X);
            h.Add(c.B);
            h.Add(c.Y);
        }
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
            h.Add(t.DistanceM);
            h.Add(t.OffBowDeg);
            h.Add(t.ShadowOf);
            h.Add(t.StationOn);
            h.Add(t.StationAstern);
            h.Add(t.StationAbeam);
            h.Add(t.LastWaypoint);
            h.Add(t.StartMs);
            h.Add(t.QuietMs);
            h.Add(t.Hand);
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
        Unqueue(new Unit(tasks[i].Unit));
        Array.Copy(tasks, i + 1, tasks, i, count - i - 1);
        tasks[--count] = default;
    }
}
