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
    LoopRoute,
    Clearance,
}

/// <summary>The world's ships (docs/battle/plan.md step 2.2), in the fine clock tier: each pose moves on the fixed
/// tick (<see cref="SimSettings.ShipTickMs"/>, on a grid of world time), whatever zoom band the map shows it at.
/// Groups, fleet symbols and the map's markers are all derived from these ships, never the other way round.
/// While there are no ships it schedules nothing, so an empty world costs nothing at any speed.
/// <para>The world registers it first, so its id is always <see cref="Id"/>.</para></summary>
public sealed partial class ShipSystem : IWorldSystem
{
    public const byte Id = 1;
    const ushort Tick = 1, RailsWaypoint = 2, PolarEntry = 3, PolarExit = 4, ContactCheck = 5, ContactScan = 6, LandAhead = 7, LandRecheck = 8;

    readonly SimSettings settings;
    readonly Relations relations;
    readonly Encounters encounters;
    readonly Sightings sightings;
    readonly List<int> used = [];   // the registry ids ships have been made of, in the order first used
    readonly Dictionary<int, Handling> handlings = [];
    readonly List<(int Ship, double AsternM, double AbeamM)> line = new(16), derailing = new(16), riders = new(16);   // a unit's ships and stations, reused (one each: they nest)
    long nextTickMs = -1;   // the tick scheduled, or -1
    int nextEntity = 1;

    public ShipStore Store { get; }

    internal ShipSystem(SimSettings settings, ShipClassRegistry registry, Probes probes, Relations relations, Encounters encounters,
        Sightings sightings)
    {
        this.settings = settings;
        this.relations = relations;
        this.encounters = encounters;
        this.sightings = sightings;
        Registry = registry;
        Store = new ShipStore(settings);
        this.probes = probes;
        stepProbe = probes.Register("Ships: step");
        contactsProbe = probes.Register("Ships: contacts");
        railsProbe = probes.Register("Ships: rails check");
        railsEventProbe = probes.Register("Ships: rails events");
        landProbe = probes.Register("Ships: land");
        encountersProbe = probes.Register("Encounters");
        sightingProbe = probes.Register("Sighting");
    }

    readonly Probes probes;
    readonly int stepProbe, contactsProbe, railsProbe, railsEventProbe, landProbe, encountersProbe, sightingProbe;

    /// <summary>The registry ids ships have been made of, in the order first used: the classes in the world.</summary>
    public IReadOnlyList<int> ClassesInUse => used;

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

    /// <summary>The entity id the latest spawn got (ids are given in order, one per ship made), 0 before any.</summary>
    public int NewestEntity => nextEntity - 1;

    /// <summary>The class id of a live ship's slot.</summary>
    public int ClassOf(int slot) => Store[slot].ClassIndex;

    // ------------------------------------------------------------------ commands

    /// <summary>A ship of a class (its registry id) at a place, heading and speed, on a side (ships of sides at war
    /// oppose each other, <see cref="Relations"/>); it gets the next entity id. A class that didn't build makes no ship. On land or near it, the ship
    /// is put at the nearest sea at its standoff; nowhere near the sea, there's no ship. (A spawn names no entity, so
    /// the command's entity field carries the side.)</summary>
    public static Command Spawn(int classIndex, double latDeg, double lonDeg, double headingDeg, double kn, byte side = 0) =>
        new(Id, (ushort)ShipCommand.Spawn, side, A: Earth.PackLatLon(latDeg, lonDeg), X: headingDeg, B: classIndex, Y: kn);

    public static Command Drop(int entity) => new(Id, (ushort)ShipCommand.Drop, entity);

    /// <summary>Steer a course, degrees; the rudder no more than given (degrees; 0 for the standard rudder).</summary>
    public static Command Course(int entity, double headingDeg, double rudderDeg = 0) =>
        new(Id, (ushort)ShipCommand.Course, entity, X: headingDeg, Y: rudderDeg);

    /// <summary>Ring a speed, knots; negative is astern.</summary>
    public static Command Speed(int entity, double kn) => new(Id, (ushort)ShipCommand.Speed, entity, X: kn);

    /// <summary>Hold the rudder at an angle, degrees, positive to starboard.</summary>
    public static Command Rudder(int entity, double deg) => new(Id, (ushort)ShipCommand.Rudder, entity, X: deg);

    public static Command ClearRoute(int entity) => new(Id, (ushort)ShipCommand.ClearRoute, entity);

    /// <summary>Add a waypoint to a ship's route and steer by the route. A route is several of these at one moment. One
    /// on land or near it is moved to the nearest sea at the ship's standoff (the map refuses the player's on land).</summary>
    public static Command AddWaypoint(int entity, double latDeg, double lonDeg) =>
        new(Id, (ushort)ShipCommand.AddWaypoint, entity, A: Earth.PackLatLon(latDeg, lonDeg));

    /// <summary>Whether a ship's route goes round again after its last waypoint (a patrol), until it's cleared.</summary>
    public static Command LoopRoute(int entity, bool loop = true) => new(Id, (ushort)ShipCommand.LoopRoute, entity, X: loop ? 1 : 0);

    /// <summary>How far from the coast the ship must keep, metres, beyond its own standoff: the half-width of the
    /// formation it leads (docs/battle/plan.md step 2.6, for the pilot of docs/world/terrain-plan.md). A setting, not a
    /// movement: it takes nothing off rails.</summary>
    public static Command Clearance(int entity, double metres) => new(Id, (ushort)ShipCommand.Clearance, entity, X: metres);

    public void Apply(World world, in Command c)
    {
        const double Deg = Math.PI / 180;
        var code = (ShipCommand)c.Code;
        if (code == ShipCommand.Spawn)
        {
            if (c.B < 0 || c.B >= Registry.Count)
                throw new ArgumentException($"no ship class {c.B}");
            if (c.Entity is < 0 or > byte.MaxValue)
                throw new ArgumentException($"no side {c.Entity}");
            if (Registry.Get(c.B) == null)
                return;   // the design didn't build: no ship, the same on every replay
            var (lat, lon) = Earth.UnpackLatLon(c.A);
            var p = Earth.Position(lat, lon);
            var h = HandlingOf(c.B);
            if (world.Shore is { } shore && !Slide.ToSea(shore, ref p, h.StandoffM, settings.CoastSearchM, out _))
                return;   // nowhere near the sea: no ship, the same on every replay
            Store.Spawn(nextEntity++, c.B, h, p, Earth.Forward(p, c.X), c.Y * Handling.MsPerKnot, world.Time.Ms, (byte)c.Entity);
            SideCount((byte)c.Entity, +1);
            EnsureTick(world);
            return;
        }
        int slot = Store.SlotOf(c.Entity);
        if (slot < 0)
            return;   // dropped or never made: an order that reached no ship

        // an order to a ship on rails: back to the fine tick first, its group with it; or carried out at once on rails
        // (the ship alone comes off for the moment, its group riding it as it is): ShipSystem.Rails.cs
        bool snap = CanSnap(world, slot, code, c.X);
        if (snap)
            Store.Derail(slot);
        else if (TakesOffRails(code))
            Derail(world, c.Entity);
        switch (code)
        {
            case ShipCommand.Clearance:
                Store.SetClearance(slot, Math.Max(c.X, 0));
                break;
            case ShipCommand.Drop:
                SideCount(Store[slot].Side, -1);
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
                var wp = Earth.Position(lat, lon);
                if (world.Shore is { } sh && !Slide.ToSea(sh, ref wp, Store[slot].Handling.StandoffM, settings.CoastSearchM, out _))
                    break;   // a waypoint nowhere near the sea is dropped (the map refuses the player's on land)
                Store.AddWaypoint(slot, wp);
                break;
            case ShipCommand.LoopRoute:
                Store.LoopRoute(slot, c.X != 0);
                break;
            default:
                throw new ArgumentException($"unknown ship command {c.Code}");
        }
        if (snap)
            Snap(world, slot);
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
        long t = Probes.Now;
        if (e.Code == ContactCheck)
        {
            CheckPair(world, e);
            probes.Add(contactsProbe, t);
            return;
        }
        if (e.Code == ContactScan)
        {
            Scan(world, e);
            probes.Add(contactsProbe, t);
            return;
        }
        if (e.Code is RailsWaypoint or PolarEntry or PolarExit)
        {
            RailsEvent(world, e);
            probes.Add(railsEventProbe, t);
            return;
        }
        if (e.Code is LandAhead or LandRecheck)
        {
            LandEvent(world, e);
            probes.Add(landProbe, t);
            return;
        }
        nextTickMs = -1;
        ShipMotion.StepAll(Store, world.Time.Ms, settings, world.Shore);
        probes.Add(stepProbe, t);
        if (world.Time.Ms % settings.RailsCheckMs == 0)
        {
            if (settings.RailsEnabled)
            {
                t = Probes.Now;
                FineContacts(world);
                probes.Add(contactsProbe, t);
            }
            UpdateEncounters(world);
            sightings.Forget(world.Time.Ms);
            if (settings.RailsEnabled)
            {
                t = Probes.Now;
                RailsCheck(world);
                probes.Add(railsProbe, t);
            }
        }
        if (encounters.Count > 0 && world.Time.Ms % settings.SightingMs == 0)
        {
            t = Probes.Now;
            sightings.Look(world, Store, used);
            probes.Add(sightingProbe, t);
        }
        if (AnyFine())
            EnsureTick(world);
    }

    public void Hash(ref StateHash h)
    {
        h.Add(nextTickMs);
        h.Add(nextScanMs);
        h.Add(scanGen);
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

    /// <summary>Copies what the screens need of every live ship of the shown sides into a snapshot's part; allocates
    /// only to grow it.</summary>
    public void Fill(ShipsView v, bool[] shownSides)
    {
        var s = Store;
        v.EnsureCapacity(s.Count);
        int n = 0, w = 0;
        for (int i = 0; i < s.Slots; i++)
        {
            if (!s.IsAlive(i) || !shownSides[s[i].Side])
                continue;
            var ship = s[i];
            v.Entity[n] = ship.Entity;
            v.Encounter[n] = encounters.Of(i, ship.Entity);
            v.Class[n] = ship.ClassIndex;
            v.Side[n] = ship.Side;
            v.Position[n] = ship.Position;
            v.Forward[n] = ship.Forward;
            v.Speed[n] = ship.Speed;
            v.YawRate[n] = ship.YawRate;
            v.Rudder[n] = ship.Rudder;
            v.PoseMs[n] = ship.PoseMs;
            v.Helm[n] = ship.Helm;
            v.OnRails[n] = ship.OnRails;
            v.CourseOrderDeg[n] = ship.CourseOrderDeg;
            v.SpeedOrderKn[n] = ship.SpeedOrderKn;
            v.Captain[n] = 0;   // the AI fills its own, after
            v.CaptainTarget[n] = 0;
            v.Asked[n] = false;
            v.LandSide[n] = (sbyte)ship.LandSide;
            v.Backstops[n] = ship.Backstops;
            v.MinClearanceM[n] = ship.Handling.MinClearanceM;
            v.StandoffM[n] = ship.Handling.StandoffM;
            v.LookaheadM[n] = Slide.LookaheadM(ship.Handling, ship.Speed, ship.CourseRudder, settings);
            bool loop = ship.RouteLoop && ship.RouteCount > 1;
            int left = ship.Helm != Helm.Route ? 0 : loop ? ship.RouteCount : ship.RouteCount - ship.RouteNext;
            v.EnsureWaypoints(w + left);
            v.RouteStart[n] = w;
            v.RouteCount[n] = left;
            v.RouteLoop[n] = loop && ship.Helm == Helm.Route;
            for (int k = 0; k < left; k++)
                v.Waypoints[w++] = ship.Waypoint((ship.RouteNext + k) % ship.RouteCount);
            n++;
        }
        v.Count = n;
        v.WaypointCount = w;
    }
}
