using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Combat;

/// <summary>What steers a ship: nothing (the rudder stays where it is), a rudder angle held, a course, or a route.</summary>
public enum Helm : byte
{
    None,
    Rudder,
    Course,
    Route,
}

/// <summary>The world's ships at sea: one slot per ship, its state in columns (docs/battle/plan.md "The shape": callers
/// reach a ship through <see cref="ShipState"/> and index accessors, never the columns' layout, so the storage can
/// become an ECS without touching them). Ships are made and dropped one at a time, at any moment: a dropped slot goes on
/// a free list and its generation goes up, so a stale handle or event is recognised. The columns grow only when a
/// spawn needs room; nothing else allocates.</summary>
public sealed class ShipStore
{
    readonly SimSettings settings;
    readonly int routeCap;
    readonly Stack<int> free = new();
    int high;   // slots in use or freed: [0, high)

    // identity
    internal int[] entity = [], generation = [], classIndex = [];
    internal bool[] alive = [];
    internal Handling[] handling = [];

    // the pose: Earth-centred, at the time it was last moved to
    internal Vec3d[] position = [], forward = [];
    internal double[] speed = [], yawRate = [], rudder = [], power = [];
    internal long[] poseMs = [];

    // what's ordered
    internal Helm[] helm = [];
    internal double[] rudderOrder = [], courseDeg = [], courseRudder = [], powerOrder = [], speedOrderKn = [];
    internal Vec3d[] route = [];   // routeCap per slot
    internal int[] routeCount = [], routeNext = [];

    public ShipStore(SimSettings settings)
    {
        this.settings = settings;
        routeCap = settings.RouteCapacity;
        Grow(Math.Max(settings.ShipCapacity, 1));
    }

    /// <summary>Slots in use or freed; iterate [0, Slots) and skip the dead.</summary>
    public int Slots => high;
    public int Count => high - free.Count;
    public int RouteCapacity => routeCap;

    public bool IsAlive(int slot) => slot >= 0 && slot < high && alive[slot];
    public ShipState this[int slot] => new(this, slot);

    /// <summary>Makes a ship of a class (its index in the world's table, and its handling) at a pose. Its helm holds the heading it's given, its engines the speed.</summary>
    public int Spawn(int entityId, int classIndex, Handling h, Vec3d position, Vec3d forward, double speedMs, long timeMs)
    {
        int slot;
        if (free.Count > 0)
            slot = free.Pop();
        else
        {
            if (high == alive.Length)
                Grow(alive.Length * 2);
            slot = high++;
        }
        var up = position.Normalized();
        entity[slot] = entityId;
        this.classIndex[slot] = classIndex;
        alive[slot] = true;
        handling[slot] = h;
        this.position[slot] = up * Earth.Radius;
        this.forward[slot] = (forward - up * Vec3d.Dot(forward, up)).Normalized();
        speed[slot] = speedMs;
        yawRate[slot] = rudder[slot] = 0;
        power[slot] = powerOrder[slot] = h.PowerFor(speedMs / Handling.MsPerKnot);
        speedOrderKn[slot] = speedMs / Handling.MsPerKnot;
        poseMs[slot] = timeMs;
        helm[slot] = Helm.Course;
        courseDeg[slot] = Earth.Heading(this.position[slot], this.forward[slot]);
        courseRudder[slot] = settings.StandardRudderDeg * Math.PI / 180;
        rudderOrder[slot] = 0;
        routeCount[slot] = routeNext[slot] = 0;
        return slot;
    }

    public void Drop(int slot)
    {
        if (!IsAlive(slot))
            return;
        alive[slot] = false;
        handling[slot] = null!;
        generation[slot]++;
        free.Push(slot);
    }

    /// <summary>The slot of a live entity, or -1. A scan: commands are rare, ticks never ask.</summary>
    public int SlotOf(int entityId)
    {
        for (int i = 0; i < high; i++)
            if (alive[i] && entity[i] == entityId)
                return i;
        return -1;
    }

    // ------------------------------------------------------------------ orders

    /// <summary>Hold a course, degrees, with a rudder no larger than given (radians; 0 for the standard rudder).</summary>
    public void OrderCourse(int slot, double courseDeg, double rudderRad = 0)
    {
        helm[slot] = Helm.Course;
        this.courseDeg[slot] = ((courseDeg % 360) + 360) % 360;
        courseRudder[slot] = rudderRad > 0 ? Math.Min(rudderRad, handling[slot].HardOverRad) : settings.StandardRudderDeg * Math.PI / 180;
    }

    /// <summary>Hold a rudder angle, radians, positive to starboard.</summary>
    public void OrderRudder(int slot, double rudderRad)
    {
        helm[slot] = Helm.Rudder;
        var h = handling[slot].HardOverRad;
        rudderOrder[slot] = Math.Clamp(rudderRad, -h, h);
    }

    /// <summary>Ring a speed, knots, negative astern: the engines go to the power that holds it.</summary>
    public void OrderSpeed(int slot, double kn)
    {
        speedOrderKn[slot] = kn;
        powerOrder[slot] = handling[slot].PowerFor(kn);
    }

    public void ClearRoute(int slot)
    {
        routeCount[slot] = routeNext[slot] = 0;
        if (helm[slot] == Helm.Route)
            OrderCourse(slot, Earth.Heading(position[slot], forward[slot]));
    }

    /// <summary>Adds a waypoint to the route and steers by it. False when the route is full.</summary>
    public bool AddWaypoint(int slot, Vec3d point)
    {
        int n = routeCount[slot];
        if (n == routeCap)
            return false;
        route[slot * routeCap + n] = point.Normalized() * Earth.Radius;
        routeCount[slot] = n + 1;
        if (helm[slot] != Helm.Route)
        {
            helm[slot] = Helm.Route;
            routeNext[slot] = n;
            courseRudder[slot] = settings.StandardRudderDeg * Math.PI / 180;
        }
        return true;
    }

    // ------------------------------------------------------------------ hashing

    public void Hash(ref StateHash h)
    {
        h.Add(high);
        h.Add(Count);
        for (int i = 0; i < high; i++)
        {
            h.Add(alive[i]);
            h.Add(generation[i]);
            if (!alive[i])
                continue;
            h.Add(entity[i]);
            h.Add(classIndex[i]);
            h.Add(position[i]);
            h.Add(forward[i]);
            h.Add(speed[i]);
            h.Add(yawRate[i]);
            h.Add(rudder[i]);
            h.Add(power[i]);
            h.Add(poseMs[i]);
            h.Add((int)helm[i]);
            h.Add(rudderOrder[i]);
            h.Add(courseDeg[i]);
            h.Add(courseRudder[i]);
            h.Add(powerOrder[i]);
            h.Add(speedOrderKn[i]);
            h.Add(routeCount[i]);
            h.Add(routeNext[i]);
            for (int k = 0; k < routeCount[i]; k++)
                h.Add(route[i * routeCap + k]);
        }
    }

    void Grow(int n)
    {
        Array.Resize(ref entity, n);
        Array.Resize(ref generation, n);
        Array.Resize(ref classIndex, n);
        Array.Resize(ref alive, n);
        Array.Resize(ref handling, n);
        Array.Resize(ref position, n);
        Array.Resize(ref forward, n);
        Array.Resize(ref speed, n);
        Array.Resize(ref yawRate, n);
        Array.Resize(ref rudder, n);
        Array.Resize(ref power, n);
        Array.Resize(ref poseMs, n);
        Array.Resize(ref helm, n);
        Array.Resize(ref rudderOrder, n);
        Array.Resize(ref courseDeg, n);
        Array.Resize(ref courseRudder, n);
        Array.Resize(ref powerOrder, n);
        Array.Resize(ref speedOrderKn, n);
        Array.Resize(ref route, n * routeCap);
        Array.Resize(ref routeCount, n);
        Array.Resize(ref routeNext, n);
    }
}

/// <summary>One ship at sea: a handle (store and slot) with accessors. Cheap to make, holds nothing of its own.</summary>
public readonly struct ShipState(ShipStore store, int slot)
{
    public int Slot => slot;
    public bool Alive => store.IsAlive(slot);
    public int Entity => store.entity[slot];
    public int Generation => store.generation[slot];
    public Handling Handling => store.handling[slot];

    /// <summary>The class's index in the world's table of classes.</summary>
    public int ClassIndex => store.classIndex[slot];

    public Vec3d Position => store.position[slot];
    public Vec3d Forward => store.forward[slot];
    public ShipFrame Frame => ShipFrame.At(store.position[slot], store.forward[slot]);

    /// <summary>Speed through the water, m/s (negative astern); rate of turn, rad/s (positive to starboard); rudder
    /// angle, rad; shaft power, W (negative astern).</summary>
    public double Speed => store.speed[slot];
    public double YawRate => store.yawRate[slot];
    public double Rudder => store.rudder[slot];
    public double Power => store.power[slot];

    /// <summary>The world time the pose is at, ms.</summary>
    public long PoseMs => store.poseMs[slot];

    public double HeadingDeg => Earth.Heading(store.position[slot], store.forward[slot]);

    public Helm Helm => store.helm[slot];
    public double CourseOrderDeg => store.courseDeg[slot];
    public double SpeedOrderKn => store.speedOrderKn[slot];
    public double RudderOrder => store.rudderOrder[slot];
    public int RouteCount => store.routeCount[slot];
    public int RouteNext => store.routeNext[slot];
    public Vec3d Waypoint(int k) => store.route[slot * store.RouteCapacity + k];
}
