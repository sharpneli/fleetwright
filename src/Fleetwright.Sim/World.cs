using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

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
    readonly List<int> systemProbes = [];   // each system's events, timed (Probes)
    readonly int commandsProbe, fillProbe;
    readonly Scheduler scheduler;
    readonly List<StampedCommand> log;
    double targetMs;

    public SimSettings Settings { get; }
    public ulong Seed { get; }

    /// <summary>The land, as the sim asks it (docs/world/terrain-plan.md): the 1:10M <see cref="Land"/> and its index.
    /// Null in a world of water (headless tests that don't need it, or must cross where land would be).</summary>
    public Shore? Shore { get; }

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

    /// <summary>Where the sim thread's time goes (docs/battle/plan.md step 2.7b): each system's events, the commands, the
    /// snapshot's fill, and the parts systems time inside themselves. Only measures; the world never reads it.</summary>
    public Probes Probes { get; } = new();

    /// <param name="classes">Where ship classes come from; a registry of its own if none (it's shared when the app's
    /// art draws from the same builds).</param>
    /// <param name="shore">The land (immutable, so the map shares it); none for a world of water.</param>
    public World(WorldTime start, ulong seed, SimSettings? settings = null, ShipClassRegistry? classes = null, Shore? shore = null)
    {
        Settings = settings ?? SimSettings.Default;
        Shore = shore;
        Seed = seed;
        Time = start;
        targetMs = start.Ms;
        scheduler = new Scheduler(Settings.EventCapacity);
        log = new List<StampedCommand>(Settings.CommandLogCapacity);
        commandsProbe = Probes.Register("Commands");
        fillProbe = Probes.Register("Snapshot fill");
        weather = new UniformWeather(new WeatherHere(Settings.DefaultVisibilityM, 0, 0));
        Encounters = new Encounters(Settings, Relations);
        Sightings = new Sightings(Settings, Relations, Encounters, seed);
        Ships = new ShipSystem(Settings, classes ?? new ShipClassRegistry(1), Probes, Relations, Encounters, Sightings);
        Sightings.Registry = Ships.Registry;
        Register(Ships);
        Groups = new GroupSystem { Store = Ships.Store };
        Register(Groups);
        ShipIndex = new ShipIndex(Ships, Settings);
        CombatLog = new CombatLog(Settings.CombatLogCapacity);
        Shells = new ShellSystem(Settings, Ships, ShipIndex, CombatLog);
        Register(Shells);
        Gunnery = new GunnerySystem(Settings, Ships, Shells, CombatLog, seed);
        Shells.Gunnery = Gunnery;
        Register(Gunnery);
        Damage = new DamageSystem(Settings, Ships, CombatLog, Sightings, seed);
        Shells.Damage = Damage;
        Register(Damage);
        Torpedoes = new TorpedoSystem(Settings, Ships, ShipIndex, CombatLog, seed) { Damage = Damage };
        Register(Torpedoes);
        Ships.ClassAdded = cls =>
        {
            Shells.Prepare(cls);
            Damage.Prepare(cls);
            Torpedoes.Prepare(cls);
        };
    }

    /// <summary>Torpedoes running (docs/battle/fighting.md 4.6): always registered sixth (<see cref="TorpedoSystem.Id"/>).</summary>
    public TorpedoSystem Torpedoes { get; }

    /// <summary>Hits and what they do (docs/battle/fighting.md 4.3a): always registered fifth
    /// (<see cref="DamageSystem.Id"/>).</summary>
    public DamageSystem Damage { get; }

    /// <summary>The batteries' fire control and firing (docs/battle/fighting.md 4.2b): always registered fourth
    /// (<see cref="GunnerySystem.Id"/>).</summary>
    public GunnerySystem Gunnery { get; }

    /// <summary>The record of the fighting: who fired, what hit (docs/battle/fighting.md 4.2b).</summary>
    public CombatLog CombatLog { get; }

    /// <summary>The world's index of where its ships are (docs/battle/fighting.md, "One index of where things are").</summary>
    public ShipIndex ShipIndex { get; }

    /// <summary>Shells in flight and their splashes (docs/battle/fighting.md 4.2a): always registered third
    /// (<see cref="ShellSystem.Id"/>).</summary>
    public ShellSystem Shells { get; }

    /// <summary>How the sides stand: war or peace (docs/battle/plan.md step 3.1a).</summary>
    public Relations Relations { get; } = new();

    /// <summary>The weather (docs/battle/plan.md step 3.2b): one for the world for now, set by a command.</summary>
    public IWeather Weather => weather;
    UniformWeather weather;

    /// <summary>The side the player commands, -1 for none (docs/battle/plan.md step 3.1c).</summary>
    public int PlayerSide { get; private set; } = -1;

    /// <summary>What the clock does when an encounter opens, by the player's rules (step 3.1c).</summary>
    public Interest Interest { get; } = new();

    /// <summary>Every navy's picture, and the looks that feed them (docs/battle/plan.md step 3.2d).</summary>
    public Sightings Sightings { get; }

    /// <summary>The encounters: ships of sides at war near each other (docs/battle/plan.md step 3.1b).</summary>
    public Encounters Encounters { get; }

    /// <summary>The world's groups: always registered second (<see cref="GroupSystem.Id"/>).</summary>
    public GroupSystem Groups { get; }

    /// <summary>The world's ships: always registered first (<see cref="ShipSystem.Id"/>).</summary>
    public ShipSystem Ships { get; }

    /// <summary>Adds a system; its id (from 1, in the order registered) goes on its events and commands. A replay
    /// registers the same systems in the same order.</summary>
    public byte Register(IWorldSystem system)
    {
        if (systems.Count == byte.MaxValue)
            throw new InvalidOperationException("too many systems");
        systems.Add(system);
        systemProbes.Add(Probes.Register(system.GetType().Name));
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

    /// <summary>Tells every system that ships came off rails (<see cref="IWorldSystem.OffRails"/>).</summary>
    internal void OffRails()
    {
        foreach (var s in systems)
            s.OffRails(this);
    }

    /// <summary>Applies a command at the current time and logs it: an input from outside (a player, a host).</summary>
    public void Apply(in Command c)
    {
        long t = Probes.Now;
        log.Add(new StampedCommand(Time, c));
        Run(in c);
        Probes.Add(commandsProbe, t);
    }

    /// <summary>Applies a command the sim gave itself (an AI's order to its ship), by the same path as
    /// <see cref="Apply"/> but not logged: a replay runs the same AI and gives it again.</summary>
    public void Issue(in Command c)
    {
        if (c.System == 0)
            throw new ArgumentException("the world's own commands come from outside");
        Run(in c);
    }

    void Run(in Command c)
    {
        Ships.Store.NowMs = Time.Ms;
        if (c.System != 0)
        {
            SystemOf(c.System).Apply(this, in c);
            foreach (var s in systems)
                s.Applied(this, in c);
            return;
        }
        switch ((WorldCommand)c.Code)
        {
            case WorldCommand.SetSpeed:
                Speed = Math.Clamp(c.X, Settings.MinSpeed, Settings.MaxSpeed);
                targetMs = Math.Min(targetMs, Time.Ms + MaxLagMs);
                Ships.SpeedChanged(this);   // the speed picks the tier
                break;
            case WorldCommand.Pause:
                Paused = true;
                targetMs = Time.Ms;
                break;
            case WorldCommand.Resume:
                Paused = false;
                targetMs = Time.Ms;
                break;
            case WorldCommand.SetRelation:
                if (c.Entity is < 0 or > byte.MaxValue || c.B is < 0 or > byte.MaxValue || c.A is < 0 or > (long)Relation.Peace)
                    throw new ArgumentException($"no relation {c.A} between sides {c.Entity} and {c.B}");
                if (Relations.Set((byte)c.Entity, (byte)c.B, (Relation)c.A))
                    Ships.RelationsChanged(this);
                break;
            case WorldCommand.SetWeather:
                if (!(c.X > 0) || !(c.Y >= 0))
                    throw new ArgumentException($"no weather of {c.X} m visibility and {c.Y} m/s wind");
                weather = new UniformWeather(new WeatherHere(c.X, ((c.A / 1000.0) % 360 + 360) % 360, c.Y));
                break;
            case WorldCommand.SetPlayerSide:
                if (c.Entity is < -1 or > byte.MaxValue)
                    throw new ArgumentException($"no side {c.Entity}");
                PlayerSide = c.Entity;
                break;
            case WorldCommand.SetInterest:
                if (c.Entity is < 0 or > (int)InterestKind.Others || c.A is < 0 or > (long)InterestAction.Slow || !(c.X > 0))
                    throw new ArgumentException($"no interest rule {c.Entity}: {c.A} at {c.X}");
                Interest.Set((InterestKind)c.Entity, new InterestRule((InterestAction)c.A, c.X));
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
        {
            Speed = Math.Clamp(s, Settings.MinSpeed, Settings.MaxSpeed);
            Ships.SpeedChanged(this);
        }
        Paused |= pause;
        targetMs = Math.Min(targetMs, Time.Ms + 1);
    }

    /// <summary>Runs events toward the target, at most about <paramref name="maxEvents"/> of them, and stopping once the
    /// wall clock passes <paramref name="deadline"/> (a <see cref="System.Diagnostics.Stopwatch"/> timestamp: the host's
    /// next snapshot is due, so a slow stretch can't hold the screen's snapshots back). Either way the events of the last
    /// moment run are finished, so the clock rests on a boundary and where it stops changes nothing the world does. True
    /// when the clock reached the target.</summary>
    public bool Advance(int maxEvents = int.MaxValue, long deadline = long.MaxValue)
    {
        int n = 0;
        long last = long.MinValue;
        while (scheduler.Count > 0)
        {
            long next = scheduler.Peek().Time.Ms;
            if (next >= Target.Ms)
                break;
            // a deadline cuts only after a moment has run (n > 0): each call makes progress, however late it starts
            if (next > last && (n >= maxEvents || n > 0 && deadline != long.MaxValue && System.Diagnostics.Stopwatch.GetTimestamp() >= deadline))
            {
                Time = new WorldTime(next);
                return false;
            }
            var e = scheduler.Pop();
            Time = e.Time;
            Ships.Store.NowMs = Time.Ms;
            last = e.Time.Ms;
            long t = Probes.Now;
            SystemOf(e.System).Handle(this, in e);
            Probes.Add(systemProbes[e.System - 1], t);
            n++;
        }
        Time = Target;
        Ships.Store.NowMs = Time.Ms;
        return true;
    }

    /// <summary>Sets the target and runs to it, on through any halt (<see cref="Halt"/>: the pause or the slower speed
    /// stays, for a host that paces by them; headless runs and tests go where they're sent).</summary>
    public void AdvanceTo(WorldTime target)
    {
        SetTarget(target);
        while (Advance() && Time < target)
            SetTarget(target);
    }

    /// <summary>Fills the world's part of a snapshot; allocates nothing. The player's view (the enemy only as their
    /// navy's contacts), or with <paramref name="truth"/> every ship (a dev's view: the host's choice, never world state,
    /// so nothing in the sim can depend on it).</summary>
    public void Fill(Snapshot s, bool truth = false)
    {
        long t = Probes.Now;
        Ships.Store.NowMs = Time.Ms;
        s.Time = Time;
        s.Target = Target;
        s.Sun = Sun.Direction(Time);
        s.Speed = Speed;
        s.Paused = Paused;
        s.PlayerSide = PlayerSide;
        s.Truth = truth;
        for (int side = 0; side < 256; side++)
            s.ShownSides[side] = truth || PlayerSide < 0 || !Relations.Hostile((byte)PlayerSide, (byte)side);
        Ships.Fill(s.Ships, s.ShownSides);
        Sightings.Fill(s.Contacts, PlayerSide, Time.Ms, Ships.Registry);
        Ships.FillSighted(s.Sighted, s.Contacts, s.ShownSides);
        s.Weather = weather.Here;
        s.InterestOwn = Interest[InterestKind.Own];
        s.InterestOthers = Interest[InterestKind.Others];
        Encounters.Fill(s.Encounters);
        foreach (var sys in systems)
            if (sys != Ships)
                sys.Fill(s);
        Probes.Add(fillProbe, t);
    }

    /// <summary>A hash of all the world's state (not the target, which is the host's pacing, nor the log).</summary>
    public ulong ComputeHash()
    {
        var h = new StateHash();
        h.Add(Seed);
        h.Add(Time.Ms);
        h.Add(Speed);
        h.Add(Paused);
        Relations.Hash(ref h);
        h.Add(PlayerSide);
        h.Add(weather.Here.VisibilityM);
        h.Add(weather.Here.WindFromDeg);
        h.Add(weather.Here.WindMs);
        Interest.Hash(ref h);
        Encounters.Hash(ref h);
        Sightings.Hash(ref h);
        scheduler.Hash(ref h);
        foreach (var s in systems)
            s.Hash(ref h);
        return h.Value;
    }

    double MaxLagMs => Settings.MaxLagRealSeconds * 1000 * Speed;

    IWorldSystem SystemOf(byte id) =>
        id >= 1 && id <= systems.Count ? systems[id - 1] : throw new InvalidOperationException($"no system {id}");
}
