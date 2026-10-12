using Fleetwright.Sim.Core;

namespace Fleetwright.Sim;

/// <summary>The world: the one root of the sim (docs/world/architecture.md). It holds the clock, the asked speed, the
/// event queue and its systems, takes commands in and fills snapshots out. It doesn't know how it's used: an exe with
/// a map, a headless test or a server all drive it the same way, from one thread at a time (<see cref="SimHost"/> is
/// the threaded host).
/// <para><b>Where the clock may rest:</b> at R only when every event before R has run and none at or after R has. So
/// a command always lands on a clean boundary, and advancing to t1 and then t2 is exactly advancing to t2: where a
/// host happens to stop never changes the world. A replay is "advance to each command's stamp, apply it".</para>
/// <para><b>The speed is the world's:</b> asked speed and pause are state, changed by commands. A host only says how
/// much real time passed (<see cref="Elapse"/>); the world turns that into its target, capped so a slow sim drops the
/// achieved speed instead of building a backlog. A system can drop the speed at the exact moment of an event
/// (<see cref="Halt"/>).</para></summary>
public sealed class World
{
    readonly List<IWorldSystem> systems = [];
    readonly Scheduler scheduler;
    readonly List<StampedCommand> log;
    double targetMs;

    public SimSettings Settings { get; }
    public ulong Seed { get; }

    /// <summary>The clock: every event before it has run, none at or after it.</summary>
    public WorldTime Time { get; private set; }

    /// <summary>How far the world may advance now; never behind <see cref="Time"/>.</summary>
    public WorldTime Target => new((long)Math.Floor(targetMs));

    /// <summary>The asked speed, game seconds per real second.</summary>
    public double Speed { get; private set; } = 1;

    public bool Paused { get; private set; }

    /// <summary>Every command applied, with the time it took effect.</summary>
    public IReadOnlyList<StampedCommand> CommandLog => log;

    public int PendingEvents => scheduler.Count;

    public World(WorldTime start, ulong seed, SimSettings? settings = null)
    {
        Settings = settings ?? SimSettings.Default;
        Seed = seed;
        Time = start;
        targetMs = start.Ms;
        scheduler = new Scheduler(Settings.EventCapacity);
        log = new List<StampedCommand>(Settings.CommandLogCapacity);
        Ships = new ShipSystem(Settings);
        Register(Ships);
    }

    /// <summary>The world's ships: always registered first (<see cref="ShipSystem.Id"/>).</summary>
    public ShipSystem Ships { get; }

    /// <summary>Adds a system; its id (from 1, in the order registered) goes on its events and commands. A replay
    /// registers the same systems in the same order.</summary>
    public byte Register(IWorldSystem system)
    {
        if (systems.Count == byte.MaxValue)
            throw new InvalidOperationException("too many systems");
        systems.Add(system);
        return (byte)systems.Count;
    }

    /// <summary>The seeded stream named for a system.</summary>
    public RandomStream Stream(string name) => RandomStream.Named(Seed, name);

    /// <summary>Schedules an event, now or later; never in the past.</summary>
    public void Schedule(WorldTime time, EventTier tier, int entity, byte system, ushort code, int generation = 0, long a = 0)
    {
        if (time < Time)
            throw new ArgumentException($"event at {time} is before the clock, {Time}");
        if (system == 0 || system > systems.Count)
            throw new ArgumentException($"no system {system}");
        scheduler.Push(time, tier, entity, system, code, generation, a);
    }

    /// <summary>Applies a command at the current time and logs it.</summary>
    public void Apply(in Command c)
    {
        log.Add(new StampedCommand(Time, c));
        if (c.System != 0)
        {
            SystemOf(c.System).Apply(this, in c);
            return;
        }
        switch ((WorldCommand)c.Code)
        {
            case WorldCommand.SetSpeed:
                Speed = Math.Clamp(c.X, Settings.MinSpeed, Settings.MaxSpeed);
                targetMs = Math.Min(targetMs, Time.Ms + MaxLagMs);
                break;
            case WorldCommand.Pause:
                Paused = true;
                targetMs = Time.Ms;
                break;
            case WorldCommand.Resume:
                Paused = false;
                targetMs = Time.Ms;
                break;
            default:
                throw new ArgumentException($"unknown world command {c.Code}");
        }
    }

    /// <summary>The host's real time passing: moves the target on at the asked speed, at most
    /// <see cref="SimSettings.MaxLagRealSeconds"/> of it ahead of the clock.</summary>
    public void Elapse(double realSeconds)
    {
        if (Paused || !(realSeconds > 0))
            return;
        targetMs = Math.Min(targetMs + realSeconds * 1000 * Speed, Time.Ms + MaxLagMs);
    }

    /// <summary>Sets the target directly, ignoring the speed: for replays and tests. A host uses
    /// <see cref="Elapse"/>.</summary>
    public void SetTarget(WorldTime target)
    {
        if (target < Time)
            throw new ArgumentException($"target {target} is before the clock, {Time}");
        targetMs = target.Ms;
    }

    /// <summary>From an event's handler: drop the speed here. The other events at this moment still run, and the
    /// clock rests 1 ms after it, paused or at a new speed.</summary>
    public void Halt(bool pause, double? speed = null)
    {
        if (speed is double s)
            Speed = Math.Clamp(s, Settings.MinSpeed, Settings.MaxSpeed);
        Paused |= pause;
        targetMs = Math.Min(targetMs, Time.Ms + 1);
    }

    /// <summary>Runs events toward the target, at most about <paramref name="maxEvents"/> of them (the events of the
    /// last moment run are finished, so the clock rests on a boundary). True when the clock reached the target.</summary>
    public bool Advance(int maxEvents = int.MaxValue)
    {
        int n = 0;
        long last = long.MinValue;
        while (scheduler.Count > 0)
        {
            long next = scheduler.Peek().Time.Ms;
            if (next >= Target.Ms)
                break;
            if (n >= maxEvents && next > last)
            {
                Time = new WorldTime(next);
                return false;
            }
            var e = scheduler.Pop();
            Time = e.Time;
            last = e.Time.Ms;
            SystemOf(e.System).Handle(this, in e);
            n++;
        }
        Time = Target;
        return true;
    }

    /// <summary>Sets the target and runs to it.</summary>
    public void AdvanceTo(WorldTime target)
    {
        SetTarget(target);
        Advance();
    }

    /// <summary>Fills the world's part of a snapshot; allocates nothing.</summary>
    public void Fill(Snapshot s)
    {
        s.Time = Time;
        s.Target = Target;
        s.Sun = Sun.Direction(Time);
        s.Speed = Speed;
        s.Paused = Paused;
        Ships.Fill(s.Ships);
    }

    /// <summary>A hash of all the world's state (not the target, which is the host's pacing, nor the log).</summary>
    public ulong ComputeHash()
    {
        var h = new StateHash();
        h.Add(Seed);
        h.Add(Time.Ms);
        h.Add(Speed);
        h.Add(Paused);
        scheduler.Hash(ref h);
        foreach (var s in systems)
            s.Hash(ref h);
        return h.Value;
    }

    double MaxLagMs => Settings.MaxLagRealSeconds * 1000 * Speed;

    IWorldSystem SystemOf(byte id) =>
        id >= 1 && id <= systems.Count ? systems[id - 1] : throw new InvalidOperationException($"no system {id}");
}
