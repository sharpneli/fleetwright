using Fleetwright.Sim.Ai.Admirals;
using Fleetwright.Sim.Ai.Captains;
using Fleetwright.Sim.Ai.Knowledge;
using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Ai;

/// <summary>The AI in the world (docs/battle/plan.md steps 2.3b, 2.4 and 2.6): one <see cref="IWorldSystem"/> holding the
/// chain of command, top-down (admirals, then captains; admiralties later), each layer ticking at its own rate on a
/// grid of world time while it has anything to watch. A host registers it after making the world. Every order to a
/// unit comes in as one of its commands (<see cref="Orders"/>), the player's logged, a layer's to the layer below not:
/// a replay registers it again and it thinks the same.</summary>
public sealed class AiSystem : IWorldSystem
{
    readonly IAiLayer[] layers;   // top-down: thought in this order at a shared moment
    readonly long[] nextTick;
    readonly int[] tickGen;   // a tick rescheduled sooner leaves the old one stale
    readonly IPicture picture;
    readonly Probes probes;
    readonly int[] layerProbes;

    public byte Id { get; }
    public AdmiralLayer Admirals { get; }
    public CaptainLayer Captains { get; }

    /// <summary>Orders to units (ships and groups), as commands to send: the player's way in, and the layers'.</summary>
    public UnitOrders Orders => new(Id);

    public AiSystem(World world)
    {
        Captains = new CaptainLayer(world.Groups);
        Admirals = new AdmiralLayer(world.Groups, Captains);
        layers = [Admirals, Captains];
        nextTick = new long[layers.Length];
        tickGen = new int[layers.Length];
        Array.Fill(nextTick, -1);
        picture = new NavyPicture(world);
        probes = world.Probes;
        layerProbes = [.. layers.Select(l => probes.Register("AI: " + l.GetType().Name))];
        Id = world.Register(this);
    }

    AiContext Context(World world) => new(world, picture, Id);

    public void Apply(World world, in Command c)
    {
        if ((AiCommand)c.Code is < AiCommand.Course or > AiCommand.Shadow)
            throw new ArgumentException($"unknown AI command {c.Code}");
        var u = new Unit(c.Entity);
        if (u.IsShip)
            Captains.Order(Context(world), c);
        else if (u.IsGroup)
            Admirals.Order(Context(world), c);
        WakeAll(world);
    }

    /// <summary>Starts the ticks of the layers that have something to watch. Groups and ships change by the world's
    /// commands, not the AI's, so this also runs after every command the world applies (<see cref="Applied"/>).</summary>
    void WakeAll(World world)
    {
        for (int i = 0; i < layers.Length; i++)
            Wake(world, i);
    }

    public void Applied(World world, in Command c)
    {
        if (c.System == ShipSystem.Id && (ShipCommand)c.Code == ShipCommand.Drop)
            Captains.Forget(c.Entity);
        WakeAll(world);
    }

    /// <summary>Ships came off rails (a contact): their captains and admirals think at their fine-tick rate again.</summary>
    public void OffRails(World world) => WakeAll(world);

    /// <summary>Schedules a layer's next think at its period now, on a grid of world time; a think already due sooner
    /// stands, one due later (a slow rails period, now a ship is back on the fine tick) is replaced.</summary>
    void Wake(World world, int layer)
    {
        if (!layers[layer].Active)
            return;
        long p = layers[layer].PeriodMs(Context(world));
        long next = (world.Time.Ms / p + 1) * p;
        if (nextTick[layer] >= 0 && nextTick[layer] <= next)
            return;
        nextTick[layer] = next;
        tickGen[layer]++;
        world.Schedule(new WorldTime(next), EventTier.Fine, layer - layers.Length, Id, (ushort)layer, tickGen[layer]);   // before the ship tick, the top layer first
    }

    /// <summary>When a layer (top-down: admirals 0, captains 1) next thinks, ms of world time, or -1 (tests).</summary>
    internal long NextThinkMs(int layer) => nextTick[layer];

    public void Handle(World world, in SimEvent e)
    {
        int layer = e.Code;
        if (e.Generation != tickGen[layer])
            return;   // replaced by a sooner one
        nextTick[layer] = -1;
        long t = Probes.Now;
        layers[layer].Think(Context(world));
        probes.Add(layerProbes[layer], t);
        WakeAll(world);   // a layer's orders can give the one below something to do (an admiral's first stations)
    }

    public void Hash(ref StateHash h)
    {
        foreach (long t in nextTick)
            h.Add(t);
        foreach (int g in tickGen)
            h.Add(g);
        foreach (var l in layers)
            l.Hash(ref h);
    }

    public void Fill(Snapshot s)
    {
        foreach (var l in layers)
            l.Fill(s);
    }
}
