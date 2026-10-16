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
    LookNow,
    Dye,
    Load,
}

/// <summary>The world's ships (docs/battle/plan.md step 2.2), in their clock tiers (ShipSystem.Tiers.cs): on the fine
/// tick each pose moves on the fixed tick (<see cref="SimSettings.ShipTickMs"/>, on a grid of world time), coarse
/// ones on segments decided for every coarse step, on rails as functions of time; whatever zoom band the map shows.
/// Groups, fleet symbols and the map's markers are all derived from these ships, never the other way round.
/// While there are no ships it schedules nothing, so an empty world costs nothing at any speed.
/// <para>The world registers it first, so its id is always <see cref="Id"/>.</para></summary>
public sealed partial class ShipSystem : IWorldSystem
{
    public const byte Id = 1;
    const ushort Tick = 1, RailsWaypoint = 2, PolarEntry = 3, PolarExit = 4, ContactCheck = 5, ContactScan = 6, LandAhead = 7, LandRecheck = 8,
        CoarseStep = 9, CoarseSegment = 10;

    readonly SimSettings settings;
    readonly Relations relations;
    readonly Encounters encounters;
    readonly Sightings sightings;
    readonly List<int> used = [];   // the registry ids ships have been made of, in the order first used
    readonly Dictionary<int, Handling> handlings = [];
    readonly List<(int Ship, double AsternM, double AbeamM)> line = new(16), derailing = new(16), riders = new(16), turning = new(16);   // a unit's ships and stations, reused (one each: they nest)
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
        coarseProbe = probes.Register("Ships: coarse");
    }

    readonly Probes probes;
    readonly int stepProbe, contactsProbe, railsProbe, railsEventProbe, landProbe, encountersProbe, sightingProbe, coarseProbe;

    /// <summary>The registry ids ships have been made of, in the order first used: the classes in the world.</summary>
    public IReadOnlyList<int> ClassesInUse => used;

    /// <summary>Where classes come from, by id: the ids <see cref="Spawn"/> names and the snapshot carries.</summary>
    public ShipClassRegistry Registry { get; }

    /// <summary>Adds a compiled class to the registry: its id (tests and tools; the game requests designs).</summary>
    public int AddClass(ShipClass c) => Registry.Add(c);

    /// <summary>A class by its registry id, waiting for its build if it must.</summary>
    public ShipClass Class(int id) => Registry.Get(id) ?? throw new InvalidOperationException($"class {id} didn't build: {Registry.ErrorOf(id)}");

    /// <summary>A ship's frame at a moment near now (a shell's window, a torpedo's next piece): on rails or coarse, its
    /// own; on the fine tick, its pose carried on along its course at its speed (seconds at most).</summary>
    public ShipFrame FrameAt(int slot, long ms)
    {
        Store.PoseAt(slot, ms, out var p, out var f);
        var ship = Store[slot];
        if (ship.Tier == ClockTier.Fine)
        {
            double d = ship.Speed * (ms - ship.PoseMs) / 1000;
            p = (p + f * d).Normalized() * Earth.Radius;
            var up = p.Normalized();
            f = (f - up * Vec3d.Dot(f, up)).Normalized();
        }
        return ShipFrame.At(p, f);
    }

    /// <summary>A class's handling in this world, made the first time a ship of it is.</summary>
    public Handling HandlingOf(int id)
    {
        if (!handlings.TryGetValue(id, out var h))
        {
            handlings[id] = h = new Handling(Class(id), settings);
            used.Add(id);
            Registry.Acquire(id);
            ClassAdded?.Invoke(Class(id));
        }
        return h;
    }

    /// <summary>Told the first time a class is in the world (the shells build its ballistic tables then).</summary>
    internal Action<ShipClass>? ClassAdded { get; set; }

    /// <summary>Told when a ship takes a loadout other than her class's (its ballistic tables are built then).</summary>
    internal Action<Loadout>? LoadoutTaken { get; set; }

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

    /// <summary>Look round now, as if the ships had been where they are a while (a dev spawn's, after its ships): opposing
    /// ships in range come off rails, encounters open, and every navy looks once over
    /// <see cref="SimSettings.SightLookNowS"/>, so what's there to see is seen at once, paused or not. A command, so a
    /// replay looks at the same moment; a batch of spawns sends one, never one a ship.</summary>
    public static Command LookNow() => new(Id, (ushort)ShipCommand.LookNow, 0);

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

    /// <summary>The dye a ship's shells carry from now on (docs/visuals/splashes.md "Dye"). A load, not a movement: it
    /// takes nothing off rails.</summary>
    public static Command Dye(int entity, Ships.Dye dye) => new(Id, (ushort)ShipCommand.Dye, entity, B: (int)dye);

    /// <summary>A ship takes a loadout of her class (its registry id, <see cref="ShipClassRegistry.AddLoadout"/>;
    /// docs/battle/shells-armour-damage.md S5), her magazines filled with it: only in port, which until the campaign's
    /// ports means before she's fired a round (CombatStore.Load); otherwise, or for another class's loadout, nothing.
    /// A load, not a movement: it takes nothing off rails.</summary>
    public static Command Load(int entity, int loadout) => new(Id, (ushort)ShipCommand.Load, entity, B: loadout);

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
            int made = Store.Spawn(nextEntity++, c.B, h, p, Earth.Forward(p, c.X), c.Y * Handling.MsPerKnot, world.Time.Ms, (byte)c.Entity);
            SideCount((byte)c.Entity, +1);
            if (coarseMode)
                ToCoarse(world, made);
            else
                EnsureTick(world);
            return;
        }
        if (code == ShipCommand.LookNow)
        {
            LookNow(world);
            return;
        }
        int slot = Store.SlotOf(c.Entity);
        if (slot < 0)
            return;   // dropped or never made: an order that reached no ship
        if (code == ShipCommand.Dye)
        {
            if (c.B is >= 0 and <= (int)Ships.Dye.Black)
                Store.SetDye(slot, (Ships.Dye)c.B);
            return;
        }
        if (code == ShipCommand.Load)
        {
            if (Registry.LoadoutOf(c.B) is { } lo && Store.Combat.Load(slot, lo) && !lo.IsDefault)
                LoadoutTaken?.Invoke(lo);
            return;
        }

        // an order to a ship on rails: back to the fine tick first, its group with it; or carried out at once on rails
        // (the ship alone comes off for the moment, its group riding it as it is): ShipSystem.Tiers.cs
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
                Normalize(world);   // its riders came off on to the fine tick: coarse again, if that's the tier
                return;
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
        else if (Store.TierOf(slot) == ClockTier.Coarse && TakesOffRails(code))
            Recoarsen(world, slot);
    }

    /// <summary>What the tick's checks do every few seconds, done now: contacts, encounters, and a look.</summary>
    void LookNow(World world)
    {
        if (settings.RailsEnabled)
            FineContacts(world);
        UpdateEncounters(world);
        if (encounters.Count > 0)
            sightings.Look(world, Store, used, settings.SightLookNowS);
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
        if (e.Code == CoarseSegment)
        {
            CoarseSegmentEnd(world, e);
            probes.Add(coarseProbe, t);
            return;
        }
        if (e.Code == CoarseStep)
        {
            CoarseStepEvent(world);
            probes.Add(coarseProbe, t);
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
            sightings.Look(world, Store, used, settings.SightingMs / 1000.0);
            probes.Add(sightingProbe, t);
        }
        if (AnyFine())
            EnsureTick(world);
    }

    public void Hash(ref StateHash h)
    {
        h.Add(nextTickMs);
        if (coarseMode || nextCoarseMs >= 0)
        {
            h.Add(coarseMode);   // only then, so a world never coarse hashes as it did before the tier
            h.Add(nextCoarseMs);
        }
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
        int n = 0, w = 0, mounts = 0;
        v.PartTotal = v.FireTotal = v.LoadTotal = 0;
        for (int i = 0; i < s.Slots; i++)
            if (s.IsAlive(i) && shownSides[s[i].Side])
                Put(v, i, n++, ref w, ref mounts, orders: true);
        v.Count = n;
        v.WaypointCount = w;
        v.MountCount = mounts;
    }

    /// <summary>The ships of the sides not shown that the player's navy has in sight (its contacts in sight), for the map
    /// to draw as they are: pose, class and mounts, no orders or route; the side is the contact's (255 until its type is
    /// made out). Allocates only to grow.</summary>
    public void FillSighted(ShipsView v, ContactsView contacts, bool[] shownSides)
    {
        v.EnsureCapacity(contacts.Count);
        int n = 0, w = 0, mounts = 0;
        v.PartTotal = v.FireTotal = v.LoadTotal = 0;
        for (int j = 0; j < contacts.Count; j++)
        {
            int i = contacts.InSight[j] ? Store.SlotOf(contacts.Entity[j]) : -1;
            if (i < 0 || shownSides[Store[i].Side])
                continue;
            Put(v, i, n, ref w, ref mounts, orders: false);
            v.Side[n++] = contacts.Side[j];
        }
        v.Count = n;
        v.WaypointCount = w;
        v.MountCount = mounts;
    }

    /// <summary>Ship slot i into row n of a snapshot's part: with its orders and route, or (<paramref name="orders"/>
    /// false) only what the eye sees.</summary>
    void Put(ShipsView v, int i, int n, ref int w, ref int mounts, bool orders)
    {
        var s = Store;
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
        v.Helm[n] = orders ? ship.Helm : Helm.Course;
        v.Tier[n] = ship.Tier;
        v.TurnEndMs[n] = Store.SegmentEndMs(i);   // a coarse ship's own; long.MaxValue for the rest
        v.CourseOrderDeg[n] = orders ? ship.CourseOrderDeg : 0;
        v.SpeedOrderKn[n] = orders ? ship.SpeedOrderKn : 0;
        v.Captain[n] = 0;   // the AI fills its own, after
        v.CaptainTarget[n] = 0;
        v.Asked[n] = false;
        v.LandSide[n] = orders ? (sbyte)ship.LandSide : (sbyte)0;
        v.Backstops[n] = orders ? ship.Backstops : 0;
        v.MinClearanceM[n] = ship.Handling.MinClearanceM;
        v.StandoffM[n] = ship.Handling.StandoffM;
        v.LookaheadM[n] = orders ? Slide.LookaheadM(ship.Handling, ship.Speed, ship.CourseRudder, settings) : 0;
        bool loop = ship.RouteLoop && ship.RouteCount > 1;
        int left = !orders || ship.Helm != Helm.Route ? 0 : loop ? ship.RouteCount : ship.RouteCount - ship.RouteNext;
        v.EnsureWaypoints(w + left);
        v.RouteStart[n] = w;
        v.RouteCount[n] = left;
        v.RouteLoop[n] = left > 0 && loop;
        for (int k = 0; k < left; k++)
            v.Waypoints[w++] = ship.Waypoint((ship.RouteNext + k) % ship.RouteCount);
        v.MountStart[n] = -1;
        if (s.Combat.Moved(i))
        {
            int count = ship.Handling.Class.Mounts.Length;
            v.EnsureMounts(mounts + count);
            v.MountStart[n] = mounts;
            for (int m = 0; m < count; m++)
                v.MountBearingDeg[mounts++] = ship.MountBearingDeg(m);
        }
        var launchers = ship.Handling.Class.Launchers;
        (v.TorpedoesLoaded[n], v.TorpedoesLeft[n]) = (0, 0);
        for (int l = 0; l < launchers.Length && orders; l++)
        {
            var st = s.Combat.Launcher(i, l, launchers[l], s.NowMs);
            v.TorpedoesLoaded[n] += st.Loaded;
            v.TorpedoesLeft[n] += st.Loaded + st.Spare;
        }
        PutLoads(v, i, n, orders);
        PutDamage(v, i, n);
    }

    /// <summary>A ship's rounds left by load, summed over each battery's mounts (own ships only).</summary>
    void PutLoads(ShipsView v, int i, int n, bool orders)
    {
        v.LoadStart[n] = -1;
        v.Loadout[n] = null;
        var lo = Store.Combat.Loadout(i);
        var cls = lo.Class;
        if (!orders || lo.LoadCount == 0)
            return;
        v.Loadout[n] = lo;
        v.EnsureLoads(v.LoadTotal + lo.LoadCount);
        int start = v.LoadStart[n] = v.LoadTotal;
        v.LoadRounds.AsSpan(start, lo.LoadCount).Clear();
        v.LoadTotal += lo.LoadCount;
        for (int b = 0; b < cls.Batteries.Length; b++)
            foreach (int m in cls.Links(cls.Batteries[b].Mounts))
                for (int k = 0; k < lo.Shells(b).Count; k++)
                {
                    ref int sum = ref v.LoadRounds[start + lo.Shells(b).Start + k];
                    int r = Store.Combat.LoadRounds(i, m, k);
                    sum = r == int.MaxValue || sum == int.MaxValue ? int.MaxValue : sum + r;
                }
    }

    /// <summary>What shows of a ship's damage (docs/battle/fighting.md 4.4): its attitude, its going down, its damaged parts
    /// and its fires.</summary>
    void PutDamage(ShipsView v, int i, int n)
    {
        var k = Store.Combat.Condition(i);
        (v.PartStart[n], v.PartCount[n], v.FireStart[n], v.FireCount[n]) = (v.PartTotal, 0, v.FireTotal, 0);
        (v.Attitude[n], v.Loss[n], v.LossMs[n], v.GoneMs[n]) = (k.Attitude, k.Loss, k.LossMs, k.GoneMs);
        if (!k.Damaged)
            return;
        int parts = k.Class.Components.Length, cells = k.CellFire.Length;
        v.EnsureDamage(v.PartTotal + parts, v.FireTotal + cells);
        for (int c = 0; c < parts; c++)
            if (k.State[c] != PartState.Ok || k.Flags[c] != PartFlags.None)
            {
                (v.Parts[v.PartTotal], v.PartStates[v.PartTotal], v.PartFlags[v.PartTotal]) = (c, k.State[c], k.Flags[c]);
                v.PartTotal++;
                v.PartCount[n]++;
            }
        for (int c = 0; c < cells; c++)
            if (k.CellFire[c])
            {
                v.FireCells[v.FireTotal++] = c;
                v.FireCount[n]++;
            }
    }
}
