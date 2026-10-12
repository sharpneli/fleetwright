using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim;

/// <summary>What the ship system understands (<see cref="ShipSystem"/>; its commands are built by its statics).</summary>
public enum ShipCommand : ushort
{
    Spawn = 1,
    Drop,
    Course,
    Speed,
    Rudder,
    ClearRoute,
    AddWaypoint,
}

/// <summary>The world's ships (docs/battle/plan.md step 2.2), in the fine clock tier: each pose moves on the fixed
/// tick (<see cref="SimSettings.ShipTickMs"/>, on a grid of world time), whatever zoom band the map shows it at.
/// Groups, fleet symbols and the map's markers are all derived from these ships, never the other way round.
/// While there are no ships it schedules nothing, so an empty world costs nothing at any speed.
/// <para>The world registers it first, so its id is always <see cref="Id"/>.</para></summary>
public sealed class ShipSystem : IWorldSystem
{
    public const byte Id = 1;
    const ushort Tick = 1;

    readonly SimSettings settings;
    readonly List<int> used = [];   // the registry ids ships have been made of, in the order first used
    readonly Dictionary<int, Handling> handlings = [];
    long nextTickMs = -1;   // the tick scheduled, or -1
    int nextEntity = 1;

    public ShipStore Store { get; }

    internal ShipSystem(SimSettings settings, ShipClassRegistry registry)
    {
        this.settings = settings;
        Registry = registry;
        Store = new ShipStore(settings);
    }

    /// <summary>Where classes come from, by id: the ids <see cref="Spawn"/> names and the snapshot carries.</summary>
    public ShipClassRegistry Registry { get; }

    /// <summary>Adds a compiled class to the registry: its id (tests and tools; the game requests designs).</summary>
    public int AddClass(ShipClass c) => Registry.Add(c);

    /// <summary>A class by its registry id, waiting for its build if it must.</summary>
    public ShipClass Class(int id) => Registry.Get(id) ?? throw new InvalidOperationException($"class {id} didn't build: {Registry.ErrorOf(id)}");

    /// <summary>A class's handling in this world, made the first time a ship of it is.</summary>
    public Handling HandlingOf(int id)
    {
        if (!handlings.TryGetValue(id, out var h))
        {
            handlings[id] = h = new Handling(Class(id), settings);
            used.Add(id);
            Registry.Acquire(id);
        }
        return h;
    }

    /// <summary>The class id of a live ship's slot.</summary>
    public int ClassOf(int slot) => Store[slot].ClassIndex;

    // ------------------------------------------------------------------ commands

    /// <summary>A ship of a class (its registry id) at a place, heading and speed; it gets the next entity id. A class
    /// that didn't build makes no ship.</summary>
    public static Command Spawn(int classIndex, double latDeg, double lonDeg, double headingDeg, double kn) =>
        new(Id, (ushort)ShipCommand.Spawn, A: Earth.PackLatLon(latDeg, lonDeg), X: headingDeg, B: classIndex, Y: kn);

    public static Command Drop(int entity) => new(Id, (ushort)ShipCommand.Drop, entity);

    /// <summary>Steer a course, degrees; the rudder no more than given (degrees; 0 for the standard rudder).</summary>
    public static Command Course(int entity, double headingDeg, double rudderDeg = 0) =>
        new(Id, (ushort)ShipCommand.Course, entity, X: headingDeg, Y: rudderDeg);

    /// <summary>Ring a speed, knots; negative is astern.</summary>
    public static Command Speed(int entity, double kn) => new(Id, (ushort)ShipCommand.Speed, entity, X: kn);

    /// <summary>Hold the rudder at an angle, degrees, positive to starboard.</summary>
    public static Command Rudder(int entity, double deg) => new(Id, (ushort)ShipCommand.Rudder, entity, X: deg);

    public static Command ClearRoute(int entity) => new(Id, (ushort)ShipCommand.ClearRoute, entity);

    /// <summary>Add a waypoint to a ship's route and steer by the route. A route is several of these at one moment.</summary>
    public static Command AddWaypoint(int entity, double latDeg, double lonDeg) =>
        new(Id, (ushort)ShipCommand.AddWaypoint, entity, A: Earth.PackLatLon(latDeg, lonDeg));

    public void Apply(World world, in Command c)
    {
        const double Deg = Math.PI / 180;
        if ((ShipCommand)c.Code == ShipCommand.Spawn)
        {
            if (c.B < 0 || c.B >= Registry.Count)
                throw new ArgumentException($"no ship class {c.B}");
            if (Registry.Get(c.B) == null)
                return;   // the design didn't build: no ship, the same on every replay
            var (lat, lon) = Earth.UnpackLatLon(c.A);
            var p = Earth.Position(lat, lon);
            Store.Spawn(nextEntity++, c.B, HandlingOf(c.B), p, Earth.Forward(p, c.X), c.Y * Handling.MsPerKnot, world.Time.Ms);
            EnsureTick(world);
            return;
        }
        int slot = Store.SlotOf(c.Entity);
        if (slot < 0)
            return;   // dropped or never made: an order that reached no ship
        switch ((ShipCommand)c.Code)
        {
            case ShipCommand.Drop:
                Store.Drop(slot);
                break;
            case ShipCommand.Course:
                Store.OrderCourse(slot, c.X, c.Y * Deg);
                break;
            case ShipCommand.Speed:
                Store.OrderSpeed(slot, c.X);
                break;
            case ShipCommand.Rudder:
                Store.OrderRudder(slot, c.X * Deg);
                break;
            case ShipCommand.ClearRoute:
                Store.ClearRoute(slot);
                break;
            case ShipCommand.AddWaypoint:
                var (lat, lon) = Earth.UnpackLatLon(c.A);
                Store.AddWaypoint(slot, Earth.Position(lat, lon));
                break;
            default:
                throw new ArgumentException($"unknown ship command {c.Code}");
        }
    }

    // ------------------------------------------------------------------ the tick

    void EnsureTick(World world)
    {
        if (nextTickMs >= 0)
            return;
        long tick = settings.ShipTickMs;
        nextTickMs = (world.Time.Ms / tick + 1) * tick;
        world.Schedule(new WorldTime(nextTickMs), EventTier.Fine, 0, Id, Tick);
    }

    public void Handle(World world, in SimEvent e)
    {
        nextTickMs = -1;
        ShipMotion.StepAll(Store, world.Time.Ms, settings);
        if (Store.Count > 0)
            EnsureTick(world);
    }

    public void Hash(ref StateHash h)
    {
        h.Add(nextTickMs);
        h.Add(nextEntity);
        h.Add(used.Count);
        foreach (int id in used)
        {
            var d = Registry.HashOf(id);
            h.Add(d.A);
            h.Add(d.B);
        }
        Store.Hash(ref h);
    }

    // ------------------------------------------------------------------ out

    /// <summary>Copies what the screens need of every live ship into a snapshot's part; allocates only to grow it.</summary>
    public void Fill(ShipsView v)
    {
        var s = Store;
        v.EnsureCapacity(s.Count);
        int n = 0, w = 0;
        for (int i = 0; i < s.Slots; i++)
        {
            if (!s.IsAlive(i))
                continue;
            var ship = s[i];
            v.Entity[n] = ship.Entity;
            v.Class[n] = ship.ClassIndex;
            v.Position[n] = ship.Position;
            v.Forward[n] = ship.Forward;
            v.Speed[n] = ship.Speed;
            v.YawRate[n] = ship.YawRate;
            v.Rudder[n] = ship.Rudder;
            v.PoseMs[n] = ship.PoseMs;
            v.Helm[n] = ship.Helm;
            v.CourseOrderDeg[n] = ship.CourseOrderDeg;
            v.SpeedOrderKn[n] = ship.SpeedOrderKn;
            int left = ship.Helm == Helm.Route ? ship.RouteCount - ship.RouteNext : 0;
            v.EnsureWaypoints(w + left);
            v.RouteStart[n] = w;
            v.RouteCount[n] = left;
            for (int k = 0; k < left; k++)
                v.Waypoints[w++] = ship.Waypoint(ship.RouteNext + k);
            n++;
        }
        v.Count = n;
        v.WaypointCount = w;
    }
}
