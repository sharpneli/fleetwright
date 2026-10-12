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
    internal byte[] side = [];
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
    internal bool[] routeLoop = [];   // past the last waypoint, on to the first again (a patrol)

    // the rails clock tier (docs/battle/plan.md step 2.5): a ship steaming steady isn't stepped, it's a function of time.
    // A ship on its own is on a great circle from an anchor (time, position, forward) at a speed; a group's other ships
    // ride at a fixed offset (ahead, to starboard) in their guide's frame. railGen goes up whenever a ship's rails
    // change, so its waypoint events go stale.
    internal bool[] rails = [];
    internal long[] railMs = [];
    internal Vec3d[] railPos = [], railFwd = [];
    internal double[] railSpeed = [], railX = [], railY = [];
    internal int[] railGuide = [], railGen = [];
    internal double[] railCourse = [];   // NaN: a great circle (a route's leg); else a rhumb line at this course

    /// <summary>The world's time: what a ship on rails is evaluated at. The world sets it before anything reads the
    /// store (events, commands, the snapshot).</summary>
    public long NowMs { get; set; }
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

    /// <summary>Makes a ship of a class (its index in the world's table, and its handling) at a pose, on a side. Its
    /// helm holds the heading it's given, its engines the speed.</summary>
    public int Spawn(int entityId, int classIndex, Handling h, Vec3d position, Vec3d forward, double speedMs, long timeMs, byte side = 0)
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
        this.side[slot] = side;
        alive[slot] = true;
        handling[slot] = h;
        this.position[slot] = up * Earth.Radius;
        this.forward[slot] = (forward - up * Vec3d.Dot(forward, up)).Normalized();
        speed[slot] = speedMs;
        yawRate[slot] = rudder[slot] = 0;
        power[slot] = powerOrder[slot] = h.PowerFor(speedMs / Handling.MsPerKnot);
        speedOrderKn[slot] = speedMs / Handling.MsPerKnot;
        poseMs[slot] = timeMs;
        SetRails(slot, false);
        railGen[slot]++;
        helm[slot] = Helm.Course;
        courseDeg[slot] = Earth.Heading(this.position[slot], this.forward[slot]);
        courseRudder[slot] = settings.StandardRudderDeg * Math.PI / 180;
        rudderOrder[slot] = 0;
        routeCount[slot] = routeNext[slot] = 0;
        routeLoop[slot] = false;
        return slot;
    }

    // ------------------------------------------------------------------ the rails tier

    public bool OnRails(int slot) => rails[slot];

    // the live ships off rails, densely: what the fine tick steps (each ship's step reads only its own slot, so their
    // order changes nothing)
    int[] fineSlots = [], fineIndex = [];
    int fineCount;

    /// <summary>How many live ships are on the fine tick, and the i-th of them (in no order that matters).</summary>
    public int FineCount => fineCount;
    public int FineSlot(int i) => fineSlots[i];

    void SetRails(int slot, bool on)
    {
        rails[slot] = on;
        if (on)
            RemoveFine(slot);
        else if (fineIndex[slot] < 0)
        {
            fineIndex[slot] = fineCount;
            fineSlots[fineCount++] = slot;
        }
    }

    void RemoveFine(int slot)
    {
        int k = fineIndex[slot];
        if (k < 0)
            return;
        int last = fineSlots[--fineCount];
        fineSlots[k] = last;
        fineIndex[last] = k;
        fineIndex[slot] = -1;
    }

    /// <summary>A ship's pose now: its stepped pose, or where its rails put it at <see cref="NowMs"/>.</summary>
    public void PoseNow(int slot, out Vec3d position, out Vec3d forward) => PoseAt(slot, NowMs, out position, out forward);

    /// <summary>Where a ship's rails put it at a time, as if they held till then (a waypoint or an order on the way
    /// re-anchors them); a ship off rails is where it was last stepped to.</summary>
    public void PoseAt(int slot, long ms, out Vec3d position, out Vec3d forward)
    {
        if (!rails[slot])
        {
            (position, forward) = (this.position[slot], this.forward[slot]);
            return;
        }
        int g = railGuide[slot];
        if (g < 0)
        {
            double s = railSpeed[slot] * (ms - railMs[slot]) / 1000.0;
            if (!double.IsNaN(railCourse[slot]))
            {
                Earth.Rhumb(railPos[slot], railCourse[slot], s, out position, out forward);
                return;
            }
            (position, forward) = (railPos[slot], railFwd[slot]);
            Earth.Advance(ref position, ref forward, s);
            return;
        }
        PoseAt(g, ms, out var gp, out var gf);
        var up = gp.Normalized();
        var p = gp + gf * railX[slot] + Vec3d.Cross(gf, up) * railY[slot];
        position = p.Normalized() * Earth.Radius;
        var u2 = position.Normalized();
        forward = (gf - u2 * Vec3d.Dot(gf, u2)).Normalized();
    }

    /// <summary>Puts a ship on rails on its own, from its pose now at a speed (the one its engines settle at: it's within
    /// a fraction of a knot of it): on a rhumb line at a course (a course held), or on the great circle along its
    /// heading (NaN).</summary>
    public void Rail(int slot, double courseDeg, double speedMs)
    {
        SetRails(slot, true);
        railGen[slot]++;
        (railMs[slot], railPos[slot], railFwd[slot], railSpeed[slot], railGuide[slot], railCourse[slot]) =
            (NowMs, position[slot], forward[slot], speedMs, -1, courseDeg);
        yawRate[slot] = rudder[slot] = 0;
    }

    /// <summary>Re-anchors a ship on rails at a point on the great circle along a direction (a route's leg; at a
    /// waypoint the turn is instantaneous).</summary>
    public void Reanchor(int slot, Vec3d at, Vec3d forward)
    {
        railGen[slot]++;
        var up = at.Normalized();
        (railMs[slot], railPos[slot], railFwd[slot], railCourse[slot]) =
            (NowMs, up * Earth.Radius, (forward - up * Vec3d.Dot(forward, up)).Normalized(), double.NaN);
    }

    /// <summary>Re-anchors a ship on rails at the start of a route's leg, a great circle from a point along a direction,
    /// and orders the leg's heading as its course: what a group placed on its stations takes from it (as passing a
    /// waypoint does).</summary>
    public void ReanchorLeg(int slot, Vec3d at, Vec3d forward)
    {
        Reanchor(slot, at, forward);
        courseDeg[slot] = Earth.Heading(railPos[slot], railFwd[slot]);
    }

    /// <summary>Re-anchors a ship on rails at a point on a rhumb line at a course (past a route's last waypoint).</summary>
    public void ReanchorRhumb(int slot, Vec3d at, double courseDeg)
    {
        railGen[slot]++;
        var up = at.Normalized();
        (railMs[slot], railPos[slot], railFwd[slot], railCourse[slot]) = (NowMs, up * Earth.Radius, Earth.Forward(up, courseDeg), courseDeg);
    }

    /// <summary>Puts a ship on rails at its offset from a guide (already on rails), as it is now.</summary>
    public void RailWith(int slot, int guide)
    {
        PoseNow(guide, out var gp, out var gf);
        var d = position[slot] - gp;
        var up = gp.Normalized();
        SetRails(slot, true);
        railGen[slot]++;
        (railGuide[slot], railX[slot], railY[slot], railSpeed[slot]) = (guide, Vec3d.Dot(d, gf), Vec3d.Dot(d, Vec3d.Cross(gf, up)), railSpeed[guide]);
        yawRate[slot] = rudder[slot] = 0;
    }

    /// <summary>Puts a ship on rails at a given offset from a guide (already on rails), metres ahead and to starboard
    /// in its frame: a formation snapped onto its stations. It takes the guide's course and speed as its orders, and
    /// the power that holds that speed, so it comes off rails steaming as the rails had it.</summary>
    public void RailAt(int slot, int guide, double aheadM, double starboardM)
    {
        SetRails(slot, true);
        railGen[slot]++;
        (railGuide[slot], railX[slot], railY[slot], railSpeed[slot]) = (guide, aheadM, starboardM, railSpeed[guide]);
        yawRate[slot] = rudder[slot] = 0;
        courseDeg[slot] = courseDeg[guide];
        speedOrderKn[slot] = railSpeed[guide] / Handling.MsPerKnot;
        power[slot] = powerOrder[slot] = handling[slot].PowerFor(speedOrderKn[slot]);
    }

    /// <summary>A ship off rails made what its orders ask at once (an order snapped on rails): its engines at the
    /// power rung and the speed that power holds, its head on the ordered course or for its next waypoint.</summary>
    public void Settle(int slot)
    {
        power[slot] = powerOrder[slot];
        speed[slot] = handling[slot].SteadySpeedMs(power[slot]);
        yawRate[slot] = rudder[slot] = 0;
        var p = position[slot];
        if (helm[slot] == Helm.Course && Math.Abs(p.Z) <= p.Length * Math.Sin(settings.PolarCapDeg * Math.PI / 180))
            forward[slot] = Earth.Forward(p, courseDeg[slot]);
        else if (helm[slot] == Helm.Route && routeNext[slot] < routeCount[slot])
        {
            var up = p.Normalized();
            var d = route[slot * routeCap + routeNext[slot]] - p;
            var dir = d - up * Vec3d.Dot(d, up);
            if (dir.Length > 1e-6)
                forward[slot] = dir.Normalized();
        }
    }

    /// <summary>On rails riding another ship (a group's ship on its guide's), not on rails of its own.</summary>
    public bool RidesAnother(int slot) => rails[slot] && railGuide[slot] >= 0;

    /// <summary>The slot of the ship a ship rides on rails (its group's guide), or -1.</summary>
    public int RidingOn(int slot) => rails[slot] ? railGuide[slot] : -1;

    /// <summary>How far a ship rides from its guide on rails, metres (0 on rails of its own, or off rails).</summary>
    public double RailOffsetM(int slot) => rails[slot] && railGuide[slot] >= 0 ? Math.Sqrt(railX[slot] * railX[slot] + railY[slot] * railY[slot]) : 0;

    /// <summary>A ship on rails has reached its route's next waypoint: it turns there at once for the one after (true,
    /// with that one), or, past the last, holds the course it arrived on (false).</summary>
    public bool PassWaypoint(int slot, out Vec3d next)
    {
        int cap = routeCap;
        var wp = route[slot * cap + routeNext[slot]];
        PoseNow(slot, out _, out var arrived);
        routeNext[slot]++;
        if (routeLoop[slot] && routeCount[slot] > 1)
            routeNext[slot] %= routeCount[slot];
        if (routeNext[slot] < routeCount[slot])
        {
            next = route[slot * cap + routeNext[slot]];
            var up = wp.Normalized();
            var d = next - wp;
            var dir = (d - up * Vec3d.Dot(d, up)).Normalized();
            Reanchor(slot, wp, dir);
            courseDeg[slot] = Earth.Heading(wp, dir);
            return true;
        }
        next = default;
        helm[slot] = Helm.Course;
        courseDeg[slot] = Earth.Heading(wp, arrived);
        ReanchorRhumb(slot, wp, courseDeg[slot]);   // the helm holds the course it arrived on: a rhumb line
        routeCount[slot] = routeNext[slot] = 0;
        routeLoop[slot] = false;
        return false;
    }

    /// <summary>A ship on rails crosses into a polar cap on its course: on from here along the great circle.</summary>
    public void EnterPolarCap(int slot)
    {
        PoseNow(slot, out var p, out var f);
        Reanchor(slot, p, f);
    }

    /// <summary>A ship on rails leaves a polar cap: it holds the heading it leaves with, as a course.</summary>
    public void LeavePolarCap(int slot)
    {
        PoseNow(slot, out var p, out var f);
        courseDeg[slot] = Earth.Heading(p, f);
        ReanchorRhumb(slot, p, courseDeg[slot]);
    }

    /// <summary>Whether a ship's rails are a rhumb line (a course held), not a great circle.</summary>
    public bool OnRhumb(int slot) => rails[slot] && railGuide[slot] < 0 && !double.IsNaN(railCourse[slot]);

    /// <summary>Counts the changes to a ship's rails: an event stamped with an older count is stale.</summary>
    public int RailGen(int slot) => railGen[slot];

    /// <summary>Takes a ship off rails, exactly where they have it now, back to the fine tick: steady, rudder
    /// amidships, its power as it was.</summary>
    public void Derail(int slot)
    {
        if (!rails[slot])
            return;
        PoseNow(slot, out var p, out var f);
        (position[slot], forward[slot], speed[slot], poseMs[slot]) = (p, f, railSpeed[slot], NowMs);
        yawRate[slot] = rudder[slot] = 0;
        if (railGuide[slot] >= 0 && helm[slot] == Helm.Course)
            courseDeg[slot] = Earth.Heading(p, f);   // a ship carried by its guide comes off steering as the rails had it, not for a course they left behind
        SetRails(slot, false);
        railGen[slot]++;
    }

    public void Drop(int slot)
    {
        if (!IsAlive(slot))
            return;
        for (int i = 0; i < high; i++)
            if (alive[i] && rails[i] && railGuide[i] == slot)
                Derail(i);   // riding on a ship that's going: off rails first
        Derail(slot);
        alive[slot] = false;
        RemoveFine(slot);   // a dead slot is stepped by nothing
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
        routeLoop[slot] = false;
        if (helm[slot] == Helm.Route)
            OrderCourse(slot, Earth.Heading(position[slot], forward[slot]));
    }

    /// <summary>Whether the route goes round again from its first waypoint after its last (a patrol), until it's
    /// cleared. A route of one waypoint doesn't loop.</summary>
    public void LoopRoute(int slot, bool loop) => routeLoop[slot] = loop;

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
            h.Add(side[i]);
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
            h.Add(routeLoop[i]);
            h.Add(rails[i]);
            h.Add(railGen[i]);
            if (rails[i])
            {
                h.Add(railMs[i]);
                h.Add(railPos[i]);
                h.Add(railFwd[i]);
                h.Add(railSpeed[i]);
                h.Add(railGuide[i]);
                h.Add(railX[i]);
                h.Add(railY[i]);
                h.Add(railCourse[i]);
            }
            for (int k = 0; k < routeCount[i]; k++)
                h.Add(route[i * routeCap + k]);
        }
    }

    void Grow(int n)
    {
        int old = fineIndex.Length;
        Array.Resize(ref fineSlots, n);
        Array.Resize(ref fineIndex, n);
        Array.Fill(fineIndex, -1, old, n - old);
        Array.Resize(ref entity, n);
        Array.Resize(ref generation, n);
        Array.Resize(ref classIndex, n);
        Array.Resize(ref side, n);
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
        Array.Resize(ref routeLoop, n);
        Array.Resize(ref rails, n);
        Array.Resize(ref railMs, n);
        Array.Resize(ref railPos, n);
        Array.Resize(ref railFwd, n);
        Array.Resize(ref railSpeed, n);
        Array.Resize(ref railX, n);
        Array.Resize(ref railY, n);
        Array.Resize(ref railGuide, n);
        Array.Resize(ref railGen, n);
        Array.Resize(ref railCourse, n);
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

    /// <summary>The side it's on: ships of different sides are opposing (relations between sides come in step 3.1).</summary>
    public byte Side => store.side[slot];

    /// <summary>Where the ship is now (on rails, evaluated at the world's time).</summary>
    public Vec3d Position
    {
        get
        {
            store.PoseNow(slot, out var p, out _);
            return p;
        }
    }

    public Vec3d Forward
    {
        get
        {
            store.PoseNow(slot, out _, out var f);
            return f;
        }
    }

    public ShipFrame Frame
    {
        get
        {
            store.PoseNow(slot, out var p, out var f);
            return ShipFrame.At(p, f);
        }
    }

    /// <summary>On the rails clock tier: a function of time, not stepped.</summary>
    public bool OnRails => store.rails[slot];

    /// <summary>Speed through the water, m/s (negative astern); rate of turn, rad/s (positive to starboard); rudder
    /// angle, rad; shaft power, W (negative astern).</summary>
    public double Speed => store.rails[slot] ? store.railSpeed[slot] : store.speed[slot];
    public double YawRate => store.yawRate[slot];
    public double Rudder => store.rudder[slot];
    public double Power => store.power[slot];

    /// <summary>The world time the pose is at, ms.</summary>
    public long PoseMs => store.rails[slot] ? store.NowMs : store.poseMs[slot];

    public double HeadingDeg
    {
        get
        {
            store.PoseNow(slot, out var p, out var f);
            return Earth.Heading(p, f);
        }
    }

    public Helm Helm => store.helm[slot];
    public double CourseOrderDeg => store.courseDeg[slot];
    public double SpeedOrderKn => store.speedOrderKn[slot];
    public double RudderOrder => store.rudderOrder[slot];

    /// <summary>The power rung (what the engines are going to), W.</summary>
    public double PowerOrder => store.powerOrder[slot];
    public int RouteCount => store.routeCount[slot];
    public int RouteNext => store.routeNext[slot];

    /// <summary>The route goes round again after its last waypoint (a patrol).</summary>
    public bool RouteLoop => store.routeLoop[slot];
    public Vec3d Waypoint(int k) => store.route[slot * store.RouteCapacity + k];
}
