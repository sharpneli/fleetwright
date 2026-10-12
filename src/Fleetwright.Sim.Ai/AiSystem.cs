using Fleetwright.Sim.Ai.Admirals;
using Fleetwright.Sim.Ai.Captains;
using Fleetwright.Sim.Ai.Knowledge;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai;

/// <summary>What the AI system understands: an order to a layer. The command's Entity, A, B, X and Y are the layer's.</summary>
public enum AiCommand : ushort
{
    /// <summary>To a ship's captain: B the <see cref="CaptainOrder"/>, A the target ship's entity, X a distance, m.</summary>
    Captain = 1,
}

/// <summary>The AI in the world (docs/battle/plan.md steps 2.3b and 2.4): one <see cref="IWorldSystem"/> holding the
/// layers of command, top-down (admirals, then captains; admiralties later), each ticking at its own rate on a grid of world
/// time while it has anything to do. A host registers it after making the world; its commands carry
/// <see cref="Id"/>. What it orders its ships isn't logged: a replay registers it again and it thinks the same.</summary>
public sealed class AiSystem : IWorldSystem
{
    readonly IAiLayer[] layers;   // top-down: thought in this order at a shared moment
    readonly long[] nextTick;
    readonly IPicture picture;

    public byte Id { get; }
    public AdmiralLayer Admirals { get; }
    public CaptainLayer Captains { get; }

    public AiSystem(World world)
    {
        Captains = new CaptainLayer();
        Admirals = new AdmiralLayer(world.Groups, Captains);
        layers = [Admirals, Captains];
        nextTick = new long[layers.Length];
        Array.Fill(nextTick, -1);
        picture = new TruthPicture(world.Ships.Store);   // EXCEPTION until pictures (step 3.2)
        Id = world.Register(this);
    }

    /// <summary>An order to a ship's captain (<see cref="CaptainOrder.None"/>: the player commands it again): a target
    /// ship, a distance astern of it and one to its starboard.</summary>
    public Command Captain(int entity, CaptainOrder order, int target = 0, double distanceM = 0, double abeamM = 0) =>
        new(Id, (ushort)AiCommand.Captain, entity, A: target, X: distanceM, B: (int)order, Y: abeamM);

    public void Apply(World world, in Command c)
    {
        var ctx = new AiContext(world, picture);
        switch ((AiCommand)c.Code)
        {
            case AiCommand.Captain:
                Captains.Order(ctx, c);
                break;
            default:
                throw new ArgumentException($"unknown AI command {c.Code}");
        }
        WakeAll(world);
    }

    /// <summary>Starts the ticks of the layers that have something to do. Groups change by the world's commands, not
    /// the AI's, so this also runs after every command the world applies (<see cref="Applied"/>).</summary>
    void WakeAll(World world)
    {
        for (int i = 0; i < layers.Length; i++)
            Wake(world, i);
    }

    public void Applied(World world, in Command c) => WakeAll(world);

    void Wake(World world, int layer)
    {
        if (nextTick[layer] >= 0 || !layers[layer].Active)
            return;
        long p = layers[layer].PeriodMs(world.Settings);
        nextTick[layer] = (world.Time.Ms / p + 1) * p;
        world.Schedule(new WorldTime(nextTick[layer]), EventTier.Fine, layer - layers.Length, Id, (ushort)layer);   // before the ship tick, the top layer first
    }

    public void Handle(World world, in SimEvent e)
    {
        int layer = e.Code;
        nextTick[layer] = -1;
        layers[layer].Think(new AiContext(world, picture));
        WakeAll(world);   // a layer's orders can give the one below something to do (an admiral's first stations)
    }

    public void Hash(ref StateHash h)
    {
        foreach (long t in nextTick)
            h.Add(t);
        foreach (var l in layers)
            l.Hash(ref h);
    }

    public void Fill(Snapshot s)
    {
        foreach (var l in layers)
            l.Fill(s.Ships);
    }
}
