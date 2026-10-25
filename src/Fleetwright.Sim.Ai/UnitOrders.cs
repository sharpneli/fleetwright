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

    /// <summary>The unit's own task (docs/battle/command.md 5.1c): its orders are the unit's own, as the player's are
    /// (the chain sees them as <see cref="Player"/>'s), but the player's own order ends the task.</summary>
    Task,

    /// <summary>A commander above the unit giving it a task: the side's commander (docs/battle/command.md 5.1d) to his
    /// top units, or a force's admiral to his members (docs/battle/admirals-orders.md O7). His tasks are tasks as the
    /// player's are, but they don't take the unit from him, as the player's do.</summary>
    Commander,
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

    /// <summary>A flotilla's torpedo attack (docs/battle/command.md 5.1b) on A, a ship (an entity, as its navy holds the
    /// contact; 0: the biggest in sight): ahead of its beam, in together, a spread on one signal, away together, and again
    /// while it has torpedoes. To a ship alone, Fight on it (4.6's attack, for a torpedo craft).</summary>
    Attack,

    /// <summary>Screen a unit (docs/battle/command.md 5.1b): A the unit screened, X how far off it, m (0: the settings').
    /// A group keeps that far off its guide toward the enemy (ahead while none is in sight), and fights enemy torpedo
    /// craft closing on it until they're beaten off. Every squadron screening a unit shares its arc
    /// (docs/battle/admirals-orders.md O1c): one on the bearing, two or more spread evenly either side of it; a force hands
    /// the order to its groups. To a ship alone, a station that far ahead of the unit.</summary>
    Screen,

    /// <summary>A task (docs/battle/command.md 5.1c, <see cref="Tasks.TaskLayer"/>): steam to A (a packed lat/lon) at X kn (0:
    /// as rung); done there.</summary>
    GoTo,

    /// <summary>A task: up and down the line from where the unit is to A (a packed lat/lon), round and round.</summary>
    Patrol,

    /// <summary>A task: steer to meet A (a ship's entity, as the navy holds the contact) at X kn (0: all the unit makes),
    /// re-solved as the picture moves; done once it's in sight and within reach of a fight.</summary>
    Intercept,

    /// <summary>A task: close on A (an own unit) and join it: a ship its squadron, a group its force (one is formed of a
    /// squadron alone); a unit detached from A goes back into its line.</summary>
    Join,

    /// <summary>A task: break off and go to A (a packed lat/lon; 0: the side's home), away from any enemy in sight first;
    /// done there.</summary>
    Withdraw,

    /// <summary>A task: scout for A (an own unit), X ahead of it (0: the settings'), Y degrees off its bow (0: dead
    /// ahead); on meeting an enemy stronger than itself, shadow him.</summary>
    Scout,

    /// <summary>A task: search about A (a packed lat/lon), legs X long (0: the settings'): a group spread abreast in a
    /// scouting line spaced by the visibility, steaming a ladder; done on contact, or at its end.</summary>
    Search,

    /// <summary>A task: cover A (an own unit), X off it (0: the settings') on the side of the threat, to support it.</summary>
    Cover,

    /// <summary>A side's commander (docs/battle/command.md 5.1d): the command's Entity is the side (not a unit), A its
    /// <see cref="Commanders.CommanderMode"/>.</summary>
    Commander,

    /// <summary>A unit under its side's commander or not (X 1: his, "leave it to the admiral"; 0: not; NaN: as its side).</summary>
    Automate,

    /// <summary>The player takes the conn of a unit (X 1) or gives it back (X 0) (docs/battle/command.md "The conn"): taken,
    /// the unit holds the course and speed ordered now, and nothing but the player's helm orders moves it: no combing a
    /// torpedo's track, no turning away, no falling out; its lookouts report the tracks instead. A general order (a
    /// behaviour, a task) or a hand-back ends it. By the player's hand only.</summary>
    Conn,

    /// <summary>Deploy (docs/battle/admirals-orders.md O2): a force into one line of battle on course X, degrees (NaN:
    /// across the course of the enemy's main body as the force sees him, as doctrine's deployment does; his bearing if
    /// he's stopped; the guide's heading if none is held), its wing column furthest along that course leading; a squadron
    /// into line ahead on it. Nothing to a ship.</summary>
    Deploy,

    /// <summary>What a unit is for (docs/battle/admirals-orders.md O6): A a <see cref="UnitRole"/> (0: worked out from its
    /// ships). A setting, as doctrine is: it doesn't take the unit from its commander.</summary>
    Role,

    /// <summary>A unit's whole line, down its tree, into a <see cref="LineShape"/> (A; docs/map-ui/battle-hud.md P1,
    /// <see cref="GroupSystem.Shape"/>): its orders stand, as a formation's do; its groups steer for the places they end
    /// in on its guide, as a deployment's do, until it's formed. Nothing to a ship.</summary>
    Shape,

    /// <summary>The player's shell order to a ship's main guns (docs/battle/shell-choice.md A5, decision 3), to a group
    /// every ship down its tree: A the <c>ShellKind</c> + 1, held against her captain's choice until those guns are out
    /// of it; 0 hands the choice back to him (by target).</summary>
    Shells,
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
/// a replay runs the AI again. Builders only: they read nothing but the AI system's id, so a UI may call them. The UI's
/// are by hand (<see cref="Hand"/>).</summary>
public readonly struct UnitOrders(byte system, bool hand = false)
{
    /// <summary>A task's command with this in its B (over the source) waits for the unit's task before it (its plan,
    /// docs/battle/command.md 5.1d), and starts when that one ends.</summary>
    public const int Queued = 1 << 8;

    /// <summary>A player's order with this in its B (over the source) is by hand, given in play: it holds the unit
    /// (docs/battle/command.md "The player's hand"): doctrine's standing orders (fight on meeting the enemy, deploy, fall
    /// out, home with empty magazines) leave it alone; only combing a torpedo's track and, under a general order (not a
    /// helm order: docs/map-ui/battle-hud.md decision 6), turning away from an attack take it for their moment. A scenario's opening orders, and a dev setup's, are the player's without it.</summary>
    public const int Hand = 1 << 9;

    /// <summary>Orders by the player's hand: what a UI gives in play.</summary>
    public UnitOrders ByHand => new(system, hand: true);

    /// <summary>A task to start when the unit's tasks before it have ended.</summary>
    public static Command Then(in Command task) => task with { B = task.B | Queued };

    public Command Course(Unit u, double headingDeg, double rudderDeg = 0, OrderSource source = OrderSource.Player) =>
        Make(AiCommand.Course, u, source, x: headingDeg, y: rudderDeg);

    public Command Speed(Unit u, double kn, OrderSource source = OrderSource.Player) => Make(AiCommand.Speed, u, source, x: kn);

    public Command Hold(Unit u, OrderSource source = OrderSource.Player) => Make(AiCommand.Hold, u, source);

    /// <summary>The player takes the conn of a unit, or gives it back: by hand only (<see cref="ByHand"/>).</summary>
    public Command Conn(Unit u, bool on) => Make(AiCommand.Conn, u, OrderSource.Player, x: on ? 1 : 0);

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

    /// <summary>A unit's whole line into one line ahead, columns or abreast, however deep it nests.</summary>
    public Command Shape(Unit u, LineShape shape, OrderSource source = OrderSource.Player) => Make(AiCommand.Shape, u, source, a: (long)shape);

    /// <summary>A unit's main guns load a kind, held against the captains' choice (null: by target, theirs again).</summary>
    public Command Shells(Unit u, Ships.ShellKind? kind, OrderSource source = OrderSource.Player) =>
        Make(AiCommand.Shells, u, source, a: kind is { } k ? (long)k + 1 : 0);

    /// <summary>Screen another unit of the same side: keep a distance off it (0: the settings'), on a station of its arc
    /// that every screen of it shares, and counter-attack enemy torpedo craft closing on it.</summary>
    public Command Screen(Unit u, Unit screened, double distanceM = 0, OrderSource source = OrderSource.Player) =>
        Make(AiCommand.Screen, u, source, a: screened.Id, x: distanceM);

    /// <summary>Deploy a force into one line of battle on a course (NaN: across the enemy's course, as doctrine's
    /// deployment); a squadron into line ahead on it.</summary>
    public Command Deploy(Unit u, double courseDeg = double.NaN, OrderSource source = OrderSource.Player) =>
        Make(AiCommand.Deploy, u, source, x: courseDeg);

    /// <summary>The task: steam to a point (at a speed, 0: as rung), done there.</summary>
    public Command GoTo(Unit u, double latDeg, double lonDeg, double kn = 0, OrderSource source = OrderSource.Player) =>
        Make(AiCommand.GoTo, u, source, a: Earth.PackLatLon(latDeg, lonDeg), x: kn);

    /// <summary>The task: up and down the line from where the unit is to a point, until told otherwise.</summary>
    public Command Patrol(Unit u, double latDeg, double lonDeg, OrderSource source = OrderSource.Player) =>
        Make(AiCommand.Patrol, u, source, a: Earth.PackLatLon(latDeg, lonDeg));

    /// <summary>The task: steer to meet a contact (a ship's entity), at a speed (0: all the unit makes).</summary>
    public Command Intercept(Unit u, int contact, double kn = 0, OrderSource source = OrderSource.Player) =>
        Make(AiCommand.Intercept, u, source, a: contact, x: kn);

    /// <summary>The task: scout ahead of an own unit (a distance, 0: the settings'; degrees off its bow).</summary>
    public Command Scout(Unit u, Unit of, double distanceM = 0, double offBowDeg = 0, OrderSource source = OrderSource.Player) =>
        Make(AiCommand.Scout, u, source, a: of.Id, x: distanceM, y: offBowDeg);

    /// <summary>The task: search about a point (legs of a length, 0: the settings').</summary>
    public Command Search(Unit u, double latDeg, double lonDeg, double legM = 0, OrderSource source = OrderSource.Player) =>
        Make(AiCommand.Search, u, source, a: Earth.PackLatLon(latDeg, lonDeg), x: legM);

    /// <summary>The task: cover an own unit (a distance, 0: the settings').</summary>
    public Command Cover(Unit u, Unit covered, double distanceM = 0, OrderSource source = OrderSource.Player) =>
        Make(AiCommand.Cover, u, source, a: covered.Id, x: distanceM);

    /// <summary>The task: close on an own unit and join it.</summary>
    public Command Join(Unit u, Unit with, OrderSource source = OrderSource.Player) => Make(AiCommand.Join, u, source, a: with.Id);

    /// <summary>The task: break off and go to a point, away from any enemy first.</summary>
    public Command Withdraw(Unit u, double latDeg, double lonDeg, OrderSource source = OrderSource.Player) =>
        Make(AiCommand.Withdraw, u, source, a: Earth.PackLatLon(latDeg, lonDeg));

    /// <summary>The task: break off and go to the side's home.</summary>
    public Command WithdrawHome(Unit u, OrderSource source = OrderSource.Player) => Make(AiCommand.Withdraw, u, source);

    /// <summary>A flotilla's torpedo attack on a contact (a ship's entity; 0: the biggest in sight).</summary>
    public Command Attack(Unit u, int contact = 0, OrderSource source = OrderSource.Player) => Make(AiCommand.Attack, u, source, a: contact);

    /// <summary>What a unit is for (<see cref="UnitRole.None"/>: back to worked out from its ships).</summary>
    public Command Role(Unit u, UnitRole role) => Make(AiCommand.Role, u, OrderSource.Player, a: (long)role);

    /// <summary>A value of a unit's doctrine (NaN: back to what it inherits from its group, its side, the default).</summary>
    public Command Doctrine(Unit u, DoctrineField field, double value) => Make(AiCommand.Doctrine, u, OrderSource.Player, a: (long)field, x: value);

    /// <summary>A value of a side's doctrine (NaN: back to the default).</summary>
    public Command SideDoctrine(int side, DoctrineField field, double value) =>
        new(system, (ushort)AiCommand.SideDoctrine, side, A: (long)field, X: value);

    /// <summary>A side's commander: none, on unless it's the player's side, on, or off.</summary>
    public Command Commander(int side, Commanders.CommanderMode mode) => new(system, (ushort)AiCommand.Commander, side, A: (long)mode);

    /// <summary>A unit handed to its side's commander (true: "leave it to the admiral"), taken from him (false), or as its
    /// side (null).</summary>
    public Command Automate(Unit u, bool? on) => Make(AiCommand.Automate, u, OrderSource.Player, x: on is { } b ? (b ? 1 : 0) : double.NaN);

    /// <summary>A side's home: where its cripples go.</summary>
    public Command Home(int side, double latDeg, double lonDeg) => new(system, (ushort)AiCommand.Home, side, A: Earth.PackLatLon(latDeg, lonDeg));

    /// <summary>A fire order an admiral hands down to a ship of his tree (<paramref name="from"/> his group): a fresh one
    /// (his group was just given it) reaches the ship whatever it holds; a redistribution only if it still holds his.</summary>
    public Command Fire(int ship, FireOrder order, int contact, int from, bool fresh) =>
        Make(order switch { FireOrder.Hold => AiCommand.HoldFire, FireOrder.Engage => AiCommand.Engage, _ => AiCommand.FireAtWill },
            Unit.Ship(ship), OrderSource.Superior, a: contact, x: fresh ? 1 : 0, y: from);

    public Command SpeedLimit(Unit u, double kn) => Make(AiCommand.SpeedLimit, u, OrderSource.Superior, x: kn);

    public Command Clearance(Unit u, double metres) => Make(AiCommand.Clearance, u, OrderSource.Superior, x: metres);

    public Command Detach(Unit u, OrderSource source = OrderSource.Player) => Make(AiCommand.Detach, u, source);

    public Command Resume(Unit u, OrderSource source = OrderSource.Player) => Make(AiCommand.Resume, u, source);

    Command Make(AiCommand code, Unit u, OrderSource source, long a = 0, double x = 0, double y = 0) =>
        new(system, (ushort)code, u.Id, A: a, X: x, B: (int)source | (hand && source == OrderSource.Player ? Hand : 0), Y: y);
}
