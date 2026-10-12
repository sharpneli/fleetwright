using Fleetwright.Sim.Ai.Knowledge;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai;

/// <summary>One layer of command (docs/battle/plan.md step 2.3b): captains (a ship each) now; admirals (a group each,
/// step 2.4) and admiralties (a navy each) later, each in a folder of its own beside Captains/. A layer thinks at its
/// own rate, on a grid of world time; layers above think first at a shared moment, so an admiral's new directive
/// reaches his captains the same second. A layer orders the layer below it, and captains order ships, through the same
/// commands a player gives (<see cref="Orders"/>). Nothing it does allocates, and all its state goes in the hash.</summary>
public interface IAiLayer
{
    /// <summary>How often it thinks now, ms of world time: its own period while any ship it commands is on the fine
    /// tick, <see cref="SimSettings.AiRailsPeriodMs"/> while they're all on rails (nothing about them changes but by
    /// events, and thinking every second would cap the clock).</summary>
    int PeriodMs(in AiContext ctx);

    /// <summary>Anything for it to do: while false its ticks stop, so an empty world stays free.</summary>
    bool Active { get; }

    void Think(in AiContext ctx);

    /// <summary>An order to this layer from the player (or the layer above, later), as a command.</summary>
    void Order(in AiContext ctx, in Command c);

    void Hash(ref StateHash h);

    /// <summary>Its part of the snapshot, per ship.</summary>
    void Fill(ShipsView v);
}

/// <summary>What a layer thinks with: the time, the settings, its own ships (it knows them fully), the picture of
/// everything else, and the way to give orders.</summary>
public readonly ref struct AiContext(World world, IPicture picture)
{
    public World World { get; } = world;
    public long NowMs => World.Time.Ms;
    public SimSettings Settings => World.Settings;
    public ShipStore Ships => World.Ships.Store;
    public IPicture Picture { get; } = picture;
    public Orders Orders => new(World);
}

/// <summary>How the AI gives a ship an order: the player's own commands, applied by the same path but not logged
/// (<see cref="World.Issue"/>), since a replay runs the AI again and it gives them again.</summary>
public readonly struct Orders(World world)
{
    public void Course(int entity, double headingDeg, double rudderDeg = 0) => world.Issue(ShipSystem.Course(entity, headingDeg, rudderDeg));
    public void Speed(int entity, double kn) => world.Issue(ShipSystem.Speed(entity, kn));
}
