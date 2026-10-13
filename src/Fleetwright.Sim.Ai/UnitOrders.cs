using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai;

/// <summary>What an order is given to (docs/battle/plan.md step 2.6): a ship (its captain) or a group (its admiral),
/// packed in one int so a command carries it: a ship's entity, or a group's id negated. 0 is nothing.</summary>
public readonly record struct Unit(int Id)
{
    public static Unit Ship(int entity) => new(entity);
    public static Unit Group(int groupId) => new(-groupId);

    public bool IsShip => Id > 0;
    public bool IsGroup => Id < 0;
    public int Entity => Id > 0 ? Id : 0;
    public int GroupId => Id < 0 ? -Id : 0;

    /// <summary>The ship a unit stands for on the sea: a ship itself, or a group's guide (0 if it has none).</summary>
    public int ShipOf(GroupSystem groups) => IsShip ? Id : IsGroup && groups.Get(-Id) is { } g ? g.Guide : 0;
}

/// <summary>Who gave an order: the player (or a unit's own; an order from nobody above) or the unit's superior in the
/// chain of command. The player's order to a unit takes it from its superior until it's handed back.</summary>
public enum OrderSource : byte
{
    Player,
    Superior,
}

/// <summary>What the AI system understands: an order to a unit (<see cref="UnitOrders"/>). The command's Entity is the
/// unit (<see cref="Unit.Id"/>), B the <see cref="OrderSource"/>; A, X and Y are the order's own.</summary>
public enum AiCommand : ushort
{
    /// <summary>X a course, degrees; Y the most rudder, degrees (0 for the standard rudder).</summary>
    Course = 1,

    /// <summary>X a speed, knots.</summary>
    Speed,

    /// <summary>Keep the course and speed ordered now (a ship mid-turn finishes its turn).</summary>
    Hold,

    ClearRoute,

    /// <summary>A packed lat/lon (<see cref="Earth.PackLatLon"/>).</summary>
    AddWaypoint,

    /// <summary>X 1 to go round the route again after its last waypoint, 0 not to.</summary>
    LoopRoute,

    /// <summary>A the unit followed; X the distance astern, m (0 for the default).</summary>
    Follow,

    /// <summary>A the unit kept station on; X the distance astern of it, Y to its starboard, m.</summary>
    Station,

    /// <summary>A the unit run from.</summary>
    Run,

    /// <summary>X the most a unit may ring, knots (0: no limit): a superior's, as he hands an order down.</summary>
    SpeedLimit,

    /// <summary>Out of its formation on purpose, until it's handed back.</summary>
    Detach,

    /// <summary>Back to its superior: a held unit takes its superior's orders again, a detached ship its station.</summary>
    Resume,

    /// <summary>X how far from the coast the unit's leading ship must keep beyond its own standoff, m: the half-width
    /// of the formation it leads, from its superior (the pilot's, docs/world/terrain-plan.md).</summary>
    Clearance,

    /// <summary>A the unit shadowed; X the range to keep, m (0: by the visibility).</summary>
    Shadow,
}

/// <summary>The one way an order gets to a unit, whoever gives it (docs/battle/plan.md step 2.6): the player's go in
/// through <see cref="World.Apply"/> and are logged; an AI layer's through <see cref="World.Issue"/> and aren't, since
/// a replay runs the AI again. Builders only: they read nothing but the AI system's id, so a UI may call them.</summary>
public readonly struct UnitOrders(byte system)
{
    public Command Course(Unit u, double headingDeg, double rudderDeg = 0, OrderSource source = OrderSource.Player) =>
        Make(AiCommand.Course, u, source, x: headingDeg, y: rudderDeg);

    public Command Speed(Unit u, double kn, OrderSource source = OrderSource.Player) => Make(AiCommand.Speed, u, source, x: kn);

    public Command Hold(Unit u, OrderSource source = OrderSource.Player) => Make(AiCommand.Hold, u, source);

    public Command ClearRoute(Unit u, OrderSource source = OrderSource.Player) => Make(AiCommand.ClearRoute, u, source);

    public Command AddWaypoint(Unit u, double latDeg, double lonDeg, OrderSource source = OrderSource.Player) =>
        Make(AiCommand.AddWaypoint, u, source, a: Earth.PackLatLon(latDeg, lonDeg));

    /// <summary>A waypoint already packed (<see cref="Earth.PackLatLon"/>): how a layer hands on one it was given.</summary>
    public Command AddWaypoint(Unit u, long packedLatLon, OrderSource source) => Make(AiCommand.AddWaypoint, u, source, a: packedLatLon);

    public Command LoopRoute(Unit u, bool loop = true, OrderSource source = OrderSource.Player) =>
        Make(AiCommand.LoopRoute, u, source, x: loop ? 1 : 0);

    public Command Follow(Unit u, Unit leader, double distanceM = 0, OrderSource source = OrderSource.Player) =>
        Make(AiCommand.Follow, u, source, a: leader.Id, x: distanceM);

    public Command Station(Unit u, Unit on, double asternM, double abeamM, OrderSource source = OrderSource.Player) =>
        Make(AiCommand.Station, u, source, a: on.Id, x: asternM, y: abeamM);

    public Command Run(Unit u, Unit from, OrderSource source = OrderSource.Player) => Make(AiCommand.Run, u, source, a: from.Id);

    /// <summary>Shadow another unit (an enemy known from the picture): keep it in sight at a range, on the bearing the
    /// shadower is on (docs/battle/plan.md step 3.2f).</summary>
    public Command Shadow(Unit u, Unit target, double rangeM = 0, OrderSource source = OrderSource.Player) =>
        Make(AiCommand.Shadow, u, source, a: target.Id, x: rangeM);

    public Command SpeedLimit(Unit u, double kn) => Make(AiCommand.SpeedLimit, u, OrderSource.Superior, x: kn);

    public Command Clearance(Unit u, double metres) => Make(AiCommand.Clearance, u, OrderSource.Superior, x: metres);

    public Command Detach(Unit u) => Make(AiCommand.Detach, u, OrderSource.Player);

    public Command Resume(Unit u) => Make(AiCommand.Resume, u, OrderSource.Player);

    Command Make(AiCommand code, Unit u, OrderSource source, long a = 0, double x = 0, double y = 0) =>
        new(system, (ushort)code, u.Id, A: a, X: x, B: (int)source, Y: y);
}
