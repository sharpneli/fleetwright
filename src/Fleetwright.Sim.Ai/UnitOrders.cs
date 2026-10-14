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

    /// <summary>Fire at will (the default): each battery picks its own target (docs/battle/fighting.md 4.2c). Y the group
    /// whose admiral handed it down (0: the unit's own, or the player's).</summary>
    FireAtWill,

    /// <summary>Hold fire: every battery ceases. Y as <see cref="FireAtWill"/>.</summary>
    HoldFire,

    /// <summary>A the ship (an entity, as its navy holds the contact) the main batteries engage; the secondaries keep
    /// picking their own. To a group, its admiral distributes it over the enemy's line. Y as <see cref="FireAtWill"/>.</summary>
    Engage,

    /// <summary>Fight: close to a range band and hold it with the main battery bearing, turning away when outgunned. A the
    /// unit fought (0: the nearest enemy), X the range, m (0: a share of the main battery's reach).</summary>
    Fight,

    /// <summary>A value of the unit's doctrine (docs/battle/command.md "Doctrine"): A the <see cref="DoctrineField"/>, X
    /// the value (NaN: back to what it inherits).</summary>
    Doctrine,

    /// <summary>A value of a side's doctrine: the command's Entity is the side (not a unit), A the field, X the value.</summary>
    SideDoctrine,

    /// <summary>Where a side's ships go when they're out of the fight (docs/battle/command.md "home"): Entity the side, A a
    /// packed lat/lon (0: none).</summary>
    Home,

    /// <summary>Turn together (docs/battle/command.md 5.1b): every ship of the unit's line turns through X degrees (+ to
    /// starboard) at once, and the line becomes a line of bearing on its old axis; Y 1 to turn the rear ship first, each
    /// the next a moment after the one astern of her (the battle turn-away, through 16 points). To a ship alone, a
    /// course X degrees from her heading.</summary>
    TurnTogether,

    /// <summary>A group's formation (A a <see cref="Formation"/>), spacing (X, m) and a line of bearing's bearing (Y,
    /// degrees): <see cref="GroupSystem.SetFormation"/> through the chain. Nothing to a ship.</summary>
    Form,
}

/// <summary>A ship's fire order (docs/battle/fighting.md 4.2c).</summary>
public enum FireOrder : byte
{
    AtWill,
    Hold,
    Engage,
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

    /// <summary>Each battery picks its own target: the nearest in sight that bears, the secondaries the smallest.</summary>
    public Command FireAtWill(Unit u, OrderSource source = OrderSource.Player) => Make(AiCommand.FireAtWill, u, source);

    public Command HoldFire(Unit u, OrderSource source = OrderSource.Player) => Make(AiCommand.HoldFire, u, source);

    /// <summary>The main batteries on a contact (a ship's entity); to a group, distributed over the enemy's line.</summary>
    public Command Engage(Unit u, int contact, OrderSource source = OrderSource.Player) => Make(AiCommand.Engage, u, source, a: contact);

    /// <summary>Close to a range band (0: by the main battery's reach) and hold it on a course that keeps the main battery
    /// bearing, turning away when outgunned; on a unit, or the nearest enemy (default).</summary>
    public Command Fight(Unit u, Unit target = default, double rangeM = 0, OrderSource source = OrderSource.Player) =>
        Make(AiCommand.Fight, u, source, a: target.Id, x: rangeM);

    /// <summary>Turn together through an angle, degrees (+ to starboard): every ship of the unit's line at once, or the
    /// rear ship first.</summary>
    public Command TurnTogether(Unit u, double deg, bool rearFirst = false, OrderSource source = OrderSource.Player) =>
        Make(AiCommand.TurnTogether, u, source, x: deg, y: rearFirst ? 1 : 0);

    /// <summary>The battle turn-away (Scheer's <i>Gefechtskehrtwendung</i>): together through 16 points, the rear ship
    /// first, to starboard or to port; the line comes out reversed, its old rear leading.</summary>
    public Command BattleTurn(Unit u, bool toStarboard, OrderSource source = OrderSource.Player) =>
        TurnTogether(u, toStarboard ? 180 : -180, rearFirst: true, source);

    /// <summary>A group's formation and spacing (and a line of bearing's bearing).</summary>
    public Command Form(Unit u, Formation formation, double spacingM, double bearingDeg = 0, OrderSource source = OrderSource.Player) =>
        Make(AiCommand.Form, u, source, a: (long)formation, x: spacingM, y: bearingDeg);

    /// <summary>A value of a unit's doctrine (NaN: back to what it inherits from its group, its side, the default).</summary>
    public Command Doctrine(Unit u, DoctrineField field, double value) => Make(AiCommand.Doctrine, u, OrderSource.Player, a: (long)field, x: value);

    /// <summary>A value of a side's doctrine (NaN: back to the default).</summary>
    public Command SideDoctrine(int side, DoctrineField field, double value) =>
        new(system, (ushort)AiCommand.SideDoctrine, side, A: (long)field, X: value);

    /// <summary>A side's home: where its cripples go.</summary>
    public Command Home(int side, double latDeg, double lonDeg) => new(system, (ushort)AiCommand.Home, side, A: Earth.PackLatLon(latDeg, lonDeg));

    /// <summary>A fire order an admiral hands down to a ship of his tree (<paramref name="from"/> his group): a fresh one
    /// (his group was just given it) reaches the ship whatever it holds; a redistribution only if it still holds his.</summary>
    public Command Fire(int ship, FireOrder order, int contact, int from, bool fresh) =>
        Make(order switch { FireOrder.Hold => AiCommand.HoldFire, FireOrder.Engage => AiCommand.Engage, _ => AiCommand.FireAtWill },
            Unit.Ship(ship), OrderSource.Superior, a: contact, x: fresh ? 1 : 0, y: from);

    public Command SpeedLimit(Unit u, double kn) => Make(AiCommand.SpeedLimit, u, OrderSource.Superior, x: kn);

    public Command Clearance(Unit u, double metres) => Make(AiCommand.Clearance, u, OrderSource.Superior, x: metres);

    public Command Detach(Unit u) => Make(AiCommand.Detach, u, OrderSource.Player);

    public Command Resume(Unit u) => Make(AiCommand.Resume, u, OrderSource.Player);

    Command Make(AiCommand code, Unit u, OrderSource source, long a = 0, double x = 0, double y = 0) =>
        new(system, (ushort)code, u.Id, A: a, X: x, B: (int)source, Y: y);
}
