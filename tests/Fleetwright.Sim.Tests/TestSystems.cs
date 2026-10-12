using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Tests;

/// <summary>A stand-in for a stepped model: a tick on a fixed grid that draws from its stream, extra events at the
/// same moment (so ties are exercised), and commands that re-anchor it (cancelling by generation) or nudge it.</summary>
sealed class Metronome : IWorldSystem
{
    public const ushort Tick = 1, Echo = 2;
    public const ushort SetPeriod = 1, Nudge = 2;

    readonly byte id;
    RandomStream rng;
    long period;
    int generation;
    public long Ticks, Echoes;
    public double Sum;

    public byte Id => id;

    public Metronome(World world, long periodMs)
    {
        rng = world.Stream("metronome");
        period = periodMs;
        id = world.Register(this);
        world.Schedule(world.Time + period, EventTier.Ships, 1, id, Tick, generation);
    }

    public void Handle(World world, in SimEvent e)
    {
        if (e.Code == Echo)
        {
            Echoes++;
            Sum = Sum * 0.999 + e.A;
            return;
        }
        if (e.Generation != generation)
            return;   // cancelled by a re-anchor
        Ticks++;
        Sum += rng.NextDouble();
        if (rng.NextInt(10) == 0)   // something else at this very moment, in another tier and for another entity
            world.Schedule(world.Time, (EventTier)rng.NextInt(4), rng.NextInt(3), id, Echo, 0, rng.NextInt(100));
        world.Schedule(world.Time + period, EventTier.Ships, 1, id, Tick, generation);
    }

    public void Apply(World world, in Command c)
    {
        if (c.Code == SetPeriod)
        {
            period = c.A;
            generation++;
            world.Schedule(world.Time + period, EventTier.Ships, 1, id, Tick, generation);
        }
        else
            Sum += c.X;
    }

    public void Hash(ref StateHash h)
    {
        rng.Hash(ref h);
        h.Add(period);
        h.Add(generation);
        h.Add(Ticks);
        h.Add(Echoes);
        h.Add(Sum);
    }
}

/// <summary>Drops the speed at one moment, as a contact will.</summary>
sealed class Halter : IWorldSystem
{
    public Halter(World world, WorldTime at, bool pause, double? speed = null)
    {
        byte id = world.Register(this);
        world.Schedule(at, EventTier.World, 0, id, 0);
        (Pause, Speed) = (pause, speed);
    }

    public bool Pause;
    public double? Speed;

    public void Handle(World world, in SimEvent e) => world.Halt(Pause, Speed);
    public void Apply(World world, in Command c) { }
    public void Hash(ref StateHash h) { }
}

/// <summary>Crashes at one moment.</summary>
sealed class Thrower : IWorldSystem
{
    public Thrower(World world, WorldTime at) => world.Schedule(at, EventTier.World, 0, world.Register(this), 0);

    public void Handle(World world, in SimEvent e) => throw new InvalidOperationException("boom");
    public void Apply(World world, in Command c) { }
    public void Hash(ref StateHash h) { }
}
