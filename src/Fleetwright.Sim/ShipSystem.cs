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
}

/// <summary>The world's ships (docs/battle/plan.md step 2.2), in the fine clock tier: each pose moves on the fixed
/// tick (<see cref="SimSettings.ShipTickMs"/>, on a grid of world time), whatever zoom band the map shows it at.
/// Groups, fleet symbols and the map's markers are all derived from these ships, never the other way round.
/// While there are no ships it schedules nothing, so an empty world costs nothing at any speed.
/// <para>The world registers it first, so its id is always <see cref="Id"/>.</para></summary>
public sealed partial class ShipSystem : IWorldSystem
{
    public const byte Id = 1;
    const ushort Tick = 1, RailsWaypoint = 2, PolarEntry = 3, PolarExit = 4, ContactCheck = 5, ContactScan = 6;

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

    /// <summary>The entity id the latest spawn got (ids are given in order, one per ship made), 0 before any.</summary>
    public int NewestEntity => nextEntity - 1;

    /// <summary>The class id of a live ship's slot.</summary>
    public int ClassOf(int slot) => Store[slot].ClassIndex;

    // ------------------------------------------------------------------ commands

    /// <summary>A ship of a class (its registry id) at a place, heading and speed, on a side (ships of different sides
    /// are opposing); it gets the next entity id. A class that didn't build makes no ship. (A spawn names no entity, so
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

    /// <summary>Add a waypoint to a ship's route and steer by the route. A route is several of these at one moment.</summary>
    public static Command AddWaypoint(int entity, double latDeg, double lonDeg) =>
        new(Id, (ushort)ShipCommand.AddWaypoint, entity, A: Earth.PackLatLon(latDeg, lonDeg));

    /// <summary>Whether a ship's route goes round again after its last waypoint (a patrol), until it's cleared.</summary>
    public static Command LoopRoute(int entity, bool loop = true) => new(Id, (ushort)ShipCommand.LoopRoute, entity, X: loop ? 1 : 0);

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
            Store.Spawn(nextEntity++, c.B, HandlingOf(c.B), p, Earth.Forward(p, c.X), c.Y * Handling.MsPerKnot, world.Time.Ms, (byte)c.Entity);
            SideCount((byte)c.Entity, +1);
            EnsureTick(world);
            return;
        }
        int slot = Store.SlotOf(c.Entity);
        if (slot < 0)
            return;   // dropped or never made: an order that reached no ship

        // an order to a ship on rails: back to the fine tick first, its group with it; or, with the clock fast enough,
        // carried out at once on rails (the ship alone comes off for the moment, its group riding it as it is)
        bool snap = CanSnap(world, slot, code, c.X);
        if (snap)
            Store.Derail(slot);
        else
            Derail(world, c.Entity);
        switch (code)
        {
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
                Store.AddWaypoint(slot, Earth.Position(lat, lon));
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

    /// <summary>Whether an order is carried out at once on rails: a course, a speed ahead or a route, to a ship on rails
    /// of its own (alone, or a group's guide), with the clock at <see cref="SimSettings.RailsSnapMinSpeed"/> or more.</summary>
    bool CanSnap(World world, int slot, ShipCommand code, double x) =>
        code is ShipCommand.Course or ShipCommand.Speed or ShipCommand.ClearRoute or ShipCommand.AddWaypoint or ShipCommand.LoopRoute &&
        (code != ShipCommand.Speed || x >= 0) && Store.OnRails(slot) && !Store.RidesAnother(slot) &&
        world.Speed >= settings.RailsSnapMinSpeed;

    /// <summary>An order carried out at once on rails: the ship takes its new course (or heads for its next waypoint)
    /// and speed where it is, and goes back on rails; its group's ships jump to their stations on the new heading.</summary>
    void Snap(World world, int slot)
    {
        Store.Settle(slot);
        RailGuide(world, slot);
        int entity = Store[slot].Entity;
        if (world.Groups.Of(entity) is { } g && g.Guide == entity)
            PlaceOnStations(g, slot);
        Anchored(world, slot);
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
        if (e.Code == ContactCheck)
        {
            CheckPair(world, e);
            return;
        }
        if (e.Code == ContactScan)
        {
            Scan(world, e);
            return;
        }
        if (e.Code is RailsWaypoint or PolarEntry or PolarExit)
        {
            int slot = Store.SlotOf(e.Entity);
            if (slot < 0 || !Store.OnRails(slot) || Store.RailGen(slot) != e.Generation)
                return;   // derailed or re-anchored since
            if (e.Code == RailsWaypoint)
                WaypointReached(world, slot);
            else if (e.Code == PolarEntry)
            {
                Store.EnterPolarCap(slot);
                SchedulePolar(world, slot);
            }
            else
            {
                Store.LeavePolarCap(slot);
                SchedulePolar(world, slot);
            }
            Anchored(world, slot);   // its rails changed: its contacts are solved again
            return;
        }
        nextTickMs = -1;
        ShipMotion.StepAll(Store, world.Time.Ms, settings);
        if (settings.RailsEnabled && world.Time.Ms % settings.RailsCheckMs == 0)
        {
            FineContacts(world);
            RailsCheck(world);
        }
        if (AnyFine())
            EnsureTick(world);
    }

    // ------------------------------------------------------------------ the rails clock tier (docs/battle/plan.md 2.5)

    /// <summary>Any ship that isn't on rails: the fine tick runs only while there is.</summary>
    bool AnyFine() => Store.FineCount > 0;

    /// <summary>Takes a ship off rails, and its group's line with it if it's in one (they ride together), back to the
    /// fine tick. A detached member rides rails of its own, so it comes off alone.</summary>
    public void Derail(World world, int entity)
    {
        bool any = false;
        if (world.Groups.Of(entity) is { } g && g.DetachmentOf(entity) == Detachment.InLine)
        {
            for (int i = 0; i < g.Line.Count; i++)
                any |= DerailSlot(Store.SlotOf(g.Line[i]));
        }
        else
            any = DerailSlot(Store.SlotOf(entity));
        if (any)
            EnsureTick(world);
    }

    bool DerailSlot(int slot)
    {
        if (slot < 0 || !Store.OnRails(slot))
            return false;
        Store.Derail(slot);
        return true;
    }

    /// <summary>Every few seconds: ships, and groups' lines whose every ship is, steaming steady go on rails. A group's
    /// guide goes on its own rails, the rest of its line at their offsets from it now, so the group keeps its shape
    /// exactly; its detached ships go on rails of their own, like ships alone.
    /// <para>With the clock at <see cref="SimSettings.RailsSnapMinSpeed"/> or more nobody watches closely, so rails are
    /// entered by placing, not by waiting: a ship or group under way on a course or a route, with no opposing ship
    /// near, is made what its orders say at once (its course, the speed its power holds) and goes on rails, its group's
    /// ships put on their stations. Forming up and settling would otherwise take minutes of fine ticks, more after an
    /// order that changed the speed.</para></summary>
    void RailsCheck(World world)
    {
        bool place = world.Speed >= settings.RailsSnapMinSpeed;
        var groups = world.Groups;
        for (int k = 0; k < groups.Count; k++)
        {
            var g = groups[k];
            int guide = Store.SlotOf(g.Guide);
            if (guide < 0 || Store.OnRails(guide))
                continue;
            if (place && Placeable(guide))
            {
                if (!NearOpposing(guide, Math.Max(GroupRadius(g, guide), FormationRadius(g))))
                {
                    Store.Settle(guide);
                    RailGuide(world, guide);
                    PlaceOnStations(g, guide);
                    Anchored(world, guide);
                }
                continue;
            }
            if (!Steady(guide))
                continue;
            var gs = Store[guide];
            bool all = true;
            for (int i = 1; i < g.Line.Count && all; i++)
            {
                // a member: not swinging, with the guide, at the guide's speed near enough (its captain re-rings speed
                // only past his deadband, so a ship in station may be rung a few tenths off; one catching up runs knots
                // faster, and isn't taken onto rails at the wrong offset)
                int m = Store.SlotOf(g.Line[i]);
                if (m < 0)
                {
                    all = false;
                    break;
                }
                var ms = Store[m];
                double dh = ms.HeadingDeg - gs.HeadingDeg;
                dh -= 360 * Math.Round(dh / 360);
                var (astern, abeam) = GroupSystem.OffsetFromGuide(g.Formation, g.SpacingM, i);
                var gp = gs.Position;
                var station = gp - gs.Forward * astern + Vec3d.Cross(gs.Forward, gp.Normalized()) * abeam;
                all = Math.Abs(ms.YawRate) * 180 / Math.PI <= settings.RailsMaxYawRateDegS && Math.Abs(ms.Rudder) * 180 / Math.PI <= settings.RailsMaxRudderDeg &&
                      Math.Abs(dh) <= settings.RailsGroupHeadingDeg && Math.Abs(ms.Speed - SteadySpeed(gs)) <= settings.RailsGroupSpeedErrKn * Handling.MsPerKnot &&
                      Earth.Distance(ms.Position, station) <= settings.RailsGroupStationM;   // in station: its offset is held on rails
            }
            if (!all || NearOpposing(guide, GroupRadius(g, guide)))
                continue;
            RailGuide(world, guide);
            for (int i = 1; i < g.Line.Count; i++)
                Store.RailWith(Store.SlotOf(g.Line[i]), guide);
            Anchored(world, guide);
        }
        for (int i = 0; i < Store.Slots; i++)
        {
            if (!Store.IsAlive(i) || Store.OnRails(i) || world.Groups.InLine(Store[i].Entity))
                continue;
            bool placed = place && Placeable(i);
            if (!placed && !Steady(i) || NearOpposing(i, 0))
                continue;
            if (placed)
                Store.Settle(i);
            RailGuide(world, i);
            Anchored(world, i);
        }
    }

    /// <summary>Whether a ship may be put on rails as its orders say, steady or not: steered on a course or a route,
    /// its engines ahead or stopped. (Damage, when it comes, will keep a ship off: a flooding or burning ship is
    /// coarse, 4.8.)</summary>
    bool Placeable(int slot)
    {
        var s = Store[slot];
        return (s.Helm == Helm.Course || s.Helm == Helm.Route && s.RouteNext < s.RouteCount) && s.PowerOrder >= 0;
    }

    /// <summary>A group's line put on rails on their stations about its guide (on rails already), on its heading.</summary>
    void PlaceOnStations(GroupSystem.Group g, int guide)
    {
        for (int i = 1; i < g.Line.Count; i++)
        {
            int m = Store.SlotOf(g.Line[i]);
            if (m < 0)
                continue;
            var (astern, abeam) = GroupSystem.OffsetFromGuide(g.Formation, g.SpacingM, i);
            Store.RailAt(m, guide, -astern, abeam);
        }
    }

    /// <summary>How far a group's stations reach from its guide, metres.</summary>
    static double FormationRadius(GroupSystem.Group g)
    {
        double r = 0;
        for (int i = 1; i < g.Line.Count; i++)
        {
            var (astern, abeam) = GroupSystem.OffsetFromGuide(g.Formation, g.SpacingM, i);
            r = Math.Max(r, Math.Sqrt(astern * astern + abeam * abeam));
        }
        return r;
    }

    /// <summary>How far a group's ships are from its guide, at most, metres.</summary>
    double GroupRadius(GroupSystem.Group g, int guide)
    {
        double r = 0;
        var gp = Store[guide].Position;
        for (int i = 1; i < g.Line.Count; i++)
            if (Store.SlotOf(g.Line[i]) is int m and >= 0)
                r = Math.Max(r, Earth.Distance(gp, Store[m].Position));
        return r;
    }

    /// <summary>A ship on its own rails: along its ordered course, or, on a route, straight for its next waypoint, with
    /// the waypoint's arrival as an event.</summary>
    void RailGuide(World world, int slot)
    {
        var s = Store[slot];
        // a course held is a rhumb line at the ordered course exactly (the heading is within a fraction of a degree of
        // it: rails turn at once, and a small error held for days would be miles), and at the speed the engines settle
        // at (within a tenth of a knot: held for days, the rest would be miles); inside a polar cap, a great circle
        bool polar = InPolarCap(s.Position);
        Store.Rail(slot, s.Helm == Helm.Course && !polar ? s.CourseOrderDeg : double.NaN, SteadySpeed(s));
        if (s.Helm == Helm.Course)
            SchedulePolar(world, slot);
        if (s.Helm == Helm.Route && s.RouteNext < s.RouteCount)
        {
            var p = s.Position;
            var wp = s.Waypoint(s.RouteNext);
            Store.ReanchorLeg(slot, p, Toward(p, wp));
            ScheduleArrival(world, slot, wp);
        }
    }

    void ScheduleArrival(World world, int slot, Vec3d wp)
    {
        var s = Store[slot];
        if (s.Speed <= 0)
            return;
        long ms = (long)Math.Ceiling(Earth.Distance(s.Position, wp) / s.Speed * 1000);
        world.Schedule(world.Time + Math.Max(ms, 1), EventTier.Rails, s.Entity, Id, RailsWaypoint, Store.RailGen(slot));
    }

    /// <summary>A ship on rails reaches a waypoint: it turns there at once for the next, or, past the last, holds the
    /// course it arrived on.</summary>
    void WaypointReached(World world, int slot)
    {
        if (Store.PassWaypoint(slot, out var next))
            ScheduleArrival(world, slot, next);
        else
            SchedulePolar(world, slot);
    }

    bool InPolarCap(Vec3d p) => Math.Abs(p.Z) > p.Length * Math.Sin(settings.PolarCapDeg * Math.PI / 180);

    /// <summary>For a ship on its own rails holding a course: when its rhumb line reaches a polar cap, or, inside one,
    /// when its great circle leaves it (solved in closed form), as an event.</summary>
    void SchedulePolar(World world, int slot)
    {
        var s = Store[slot];
        if (s.Speed <= 0 || s.Helm != Helm.Course)
            return;
        var p = s.Position;
        double cap = settings.PolarCapDeg * Math.PI / 180, dist;
        ushort code;
        if (Store.OnRhumb(slot))
        {
            // the latitude changes linearly along a rhumb line: d lat / ds = cos(course) / R
            double c = Math.Cos(s.CourseOrderDeg * Math.PI / 180), lat = Math.Asin(p.Z / p.Length);
            if (Math.Abs(c) < 1e-9)
                return;
            dist = ((c > 0 ? cap : -cap) - lat) / c * Earth.Radius;
            code = PolarEntry;
        }
        else
        {
            // on the great circle p cos u + f sin u (u = s/R), the height toward the pole is A cos(u - phi), with
            // A = |(pz, fz)| and phi = atan2(fz, pz); it falls back to the cap's edge at u = phi + acos(edge / A)
            var up = p.Normalized();
            var f = s.Forward;
            double sign = Math.Sign(up.Z), pz = up.Z * sign, fz = f.Z * sign, edge = Math.Sin(cap);
            double a = Math.Sqrt(pz * pz + fz * fz);
            dist = Earth.Radius * (Math.Atan2(fz, pz) + Math.Acos(Math.Min(edge / a, 1)));
            code = PolarExit;
        }
        if (!(dist > 0))
            return;
        long ms = (long)Math.Ceiling(dist / s.Speed * 1000);
        world.Schedule(world.Time + Math.Max(ms, 1), EventTier.Rails, s.Entity, Id, code, Store.RailGen(slot));
    }

    static Vec3d Toward(Vec3d from, Vec3d to)
    {
        var up = from.Normalized();
        var d = to - from;
        return (d - up * Vec3d.Dot(d, up)).Normalized();
    }

    /// <summary>Steaming steady: on a course or a route's leg and on it, rudder amidships, no swing, the engines at what
    /// was rung and the speed what they give.</summary>
    bool Steady(int slot)
    {
        var s = Store[slot];
        if (s.Helm is not (Helm.Course or Helm.Route) || Math.Abs(s.YawRate) * 180 / Math.PI > settings.RailsMaxYawRateDegS ||
            Math.Abs(s.Rudder) * 180 / Math.PI > settings.RailsMaxRudderDeg || s.Power < 0 || s.Power != s.PowerOrder)
            return false;
        double want = s.Helm == Helm.Course ? s.CourseOrderDeg : Earth.Bearing(s.Position, s.Waypoint(s.RouteNext));
        double dc = want - s.HeadingDeg;
        dc -= 360 * Math.Round(dc / 360);
        if (Math.Abs(dc) > settings.RailsMaxCourseErrDeg)
            return false;
        return Math.Abs(s.Speed - SteadySpeed(s)) <= settings.RailsMaxSpeedErrKn * Handling.MsPerKnot;
    }

    /// <summary>The speed a ship's power holds it at, m/s.</summary>
    static double SteadySpeed(ShipState s) => s.Handling.SteadySpeedMs(s.Power);

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
