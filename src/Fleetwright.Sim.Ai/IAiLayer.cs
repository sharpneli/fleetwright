using Fleetwright.Sim.Ai.Knowledge;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai;

/// <summary>One layer of the chain of command (docs/battle/plan.md steps 2.3b and 2.6): captains (a ship each) and
/// admirals (a group each) now; admiralties (a navy each) later, each in a folder of its own. A layer thinks at its own
/// rate, on a grid of world time; layers above think first at a shared moment, so an admiral's new directive reaches
/// his captains the same second. A layer orders only the one below it, by the same unit orders the player gives
/// (<see cref="UnitOrders"/>, through <see cref="AiContext.Issue"/>); captains alone order the helm
/// (<see cref="HelmOrders"/>). Nothing it does allocates, and all its state goes in the hash.</summary>
public interface IAiLayer
{
    /// <summary>How often it thinks now, ms of world time: its own period while any ship it watches is on the fine
    /// tick, the coarse step while any is coarse (it decides once a step there), <see cref="SimSettings.AiRailsPeriodMs"/>
    /// while they're all on rails (nothing about them changes but by events, and thinking every second would cap the
    /// clock).</summary>
    int PeriodMs(in AiContext ctx);

    /// <summary>Anything for it to watch: while false its ticks stop, so a world of ships on plain orders stays free.</summary>
    bool Active { get; }

    void Think(in AiContext ctx);

    /// <summary>An order to one of this layer's units, from the player or the layer above, as a command.</summary>
    void Order(in AiContext ctx, in Command c);

    void Hash(ref StateHash h);

    /// <summary>Its part of the snapshot, per ship and per group, after the world's.</summary>
    void Fill(Snapshot s);
}

/// <summary>What a layer thinks with: the time, the settings, its own ships (it knows them fully), the picture of
/// everything else, and the ways to give orders.</summary>
public readonly ref struct AiContext(World world, IPicture picture, byte system)
{
    public World World { get; } = world;
    public long NowMs => World.Time.Ms;
    public SimSettings Settings => World.Settings;
    public ShipStore Ships => World.Ships.Store;
    public GroupSystem Groups => World.Groups;
    public IPicture Picture { get; } = picture;

    /// <summary>Orders to units, for the layer below (<see cref="Issue"/> them).</summary>
    public UnitOrders Orders => new(system);

    /// <summary>A captain's orders to his ship's helm and engines.</summary>
    public HelmOrders Helm => new(World);

    /// <summary>Gives an order the sim made itself: applied at once by the path the player's take, not logged.</summary>
    public void Issue(in Command c) => World.Issue(c);
}

/// <summary>How a captain works his ship: the helm and engine commands of <see cref="ShipSystem"/>, applied by the same
/// path as anyone's but not logged (<see cref="World.Issue"/>), since a replay runs the AI again and it gives them
/// again. Only captains use it: everyone else orders a unit.</summary>
public readonly struct HelmOrders(World world)
{
    public void Course(int entity, double headingDeg, double rudderDeg = 0) => world.Issue(ShipSystem.Course(entity, headingDeg, rudderDeg));
    public void Speed(int entity, double kn) => world.Issue(ShipSystem.Speed(entity, kn));
    public void ClearRoute(int entity) => world.Issue(ShipSystem.ClearRoute(entity));
    public void AddWaypoint(int entity, long packedLatLon) => world.Issue(new Command(ShipSystem.Id, (ushort)ShipCommand.AddWaypoint, entity, A: packedLatLon));
    public void LoopRoute(int entity, bool loop) => world.Issue(ShipSystem.LoopRoute(entity, loop));
    public void ResumeRoute(int entity) => world.Issue(ShipSystem.ResumeRoute(entity));
}
