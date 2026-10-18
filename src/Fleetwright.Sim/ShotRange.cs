using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim;

/// <summary>One shell fired on a <see cref="ShotRange"/>: what was fired and from where (a range and an aspect, degrees
/// off the target's bow it came from, as <see cref="GivenHits"/>), its fall and striking speed in the air; where it
/// struck (or why it didn't), its own path through the ship as the hit took it (bent where it turned or glanced), the
/// terminal walk plate by plate, its outcome and effects, and what changed on the ship: parts worse, holes to the sea,
/// spaces set burning.</summary>
public sealed class Shot
{
    public ShellTypeRow Shell { get; init; }
    public string ShellName { get; init; } = "";
    public double RangeM { get; init; }
    public double AspectDeg { get; init; }
    public double FallDeg { get; init; }
    public double AirStrikeMs { get; init; }
    public ulong Seed { get; init; }
    public P3 Aim { get; init; }
    public P3 Direction { get; init; }

    /// <summary>Whether it struck the ship; if not, why (the sea took it, or it missed her).</summary>
    public bool Struck { get; internal set; }
    public string Note { get; internal set; } = "";

    /// <summary>Where it struck (the first crossing into the hull or a part), and how far it ran under the water first.</summary>
    public P3 Point { get; internal set; }
    public double WaterRunM { get; internal set; }

    public HitPath Path { get; } = new(16);
    public TerminalResult Terminal { get; internal set; }
    public EffectsResult Effects { get; internal set; }
    public HitEffect Effect { get; internal set; }

    /// <summary>The terminal walk: the plates, the striking speed at the hull, where the fuze started and why.</summary>
    public PlateStep[] Steps { get; internal set; } = [];
    public double StrikeMs { get; internal set; }
    public double FuzeT { get; internal set; } = double.NaN;
    public FuzeCause Fuze { get; internal set; }

    /// <summary>The parts (nodes of the class) it made worse: their state and flags before and after.</summary>
    public List<(int Node, PartState Was, PartState Now, PartFlags WasFlags, PartFlags NowFlags)> Parts { get; } = [];

    /// <summary>The cells it holed to the sea (m² added), and those it set burning.</summary>
    public List<(int Cell, double M2)> Holes { get; } = [];
    public List<int> Fires { get; } = [];
}

/// <summary>A range to fire single shells at a class (the hitbox viewer's shot; a tool, not world state): a world of its
/// own with one ship of the class in it, each shell through the world's own damage system (<see cref="DamageSystem.Strike"/>)
/// with a <see cref="TerminalTrace"/> listening. A fresh ship for each shot, or the same one taking them in turn.</summary>
public sealed class ShotRange(SimSettings settings)
{
    readonly TerminalTrace trace = new();
    readonly HitPath probe = new();
    World? world;
    ShipClass? cls;
    int entity;

    /// <summary>The ship's condition now (null before the first shot).</summary>
    public ShipCondition? Condition => world == null ? null : world.Ships.Store.Combat.Condition(world.Ships.Store.SlotOf(entity));

    /// <summary>The next shot comes to a fresh ship.</summary>
    public void Reset() => world = null;

    /// <summary>A shell at a range from an aspect, its line of flight through the aim (ship frame), on a fresh ship of the
    /// class (a world of the seed), or with <paramref name="keep"/> on the one the last shots struck.</summary>
    public Shot Fire(ShipClass target, in ShellTypeRow shell, string shellName, double maxElevationDeg, double rangeM, double aspectDeg, P3 aim, ulong seed,
        bool keep = false)
    {
        var (dir, fall) = GivenHits.Flight(shell, maxElevationDeg, rangeM, aspectDeg);
        if (!keep || world == null || !ReferenceEquals(cls, target))
        {
            world = new World(WorldTime.FromUtc(1916, 5, 31, 14), seed, settings);
            world.Damage.Trace = trace;
            world.Apply(ShipSystem.Spawn(world.Ships.AddClass(target), 56, 4, 0, 15));
            (entity, cls) = (world.Ships.NewestEntity, target);
        }
        var shot = new Shot
        {
            Shell = shell, ShellName = shellName, RangeM = rangeM, AspectDeg = aspectDeg, FallDeg = fall.FallDeg, AirStrikeMs = fall.StrikeMs, Seed = seed,
            Aim = aim, Direction = dir,
        };
        var k = Condition!;
        if (k.Status != ShipStatus.Afloat)
        {
            shot.Note = "she's going down: reset the ship";
            return shot;
        }

        // its line through the aim: where it meets the ship first, and whether the sea took it on the way
        var water = HitQuery.WaterOf(target, k.Attitude);
        double far = 2 * target.Solids.Radius + 50;
        ShipTracer.Trace(target, new P3(aim.X - dir.X * far, aim.Y - dir.Y * far, aim.Z - dir.Z * far),
            new P3(aim.X + dir.X * far, aim.Y + dir.Y * far, aim.Z + dir.Z * far), [], water, probe);
        int first = GivenHits.First(probe);
        if (first < 0)
        {
            shot.Note = "missed her";
            return shot;
        }
        double run = WaterRun.RunM(probe, first), reach = WaterRun.ReachM(shell, fall.StrikeMs, settings);
        if (run > reach)
        {
            shot.Note = $"the sea took it: {run:0.0} m under the water to her, its reach {reach:0.0} m";
            return shot;
        }
        (shot.Struck, shot.Point, shot.WaterRunM) = (true, probe.Crossings[first].Point, run);

        var state = (PartState[])k.State.Clone();
        var flags = (PartFlags[])k.Flags.Clone();
        var breach = (double[])k.BreachM2.Clone();
        var fire = k.Fire.Select(f => f.Burning).ToArray();
        world!.Damage.Strike(world, new Hit
        {
            Ship = entity, Ms = world.Time.Ms, Point = shot.Point, Direction = dir, SpeedMs = fall.StrikeMs, WaterRunM = run, Source = HitSource.Shell,
            Shell = shell, ShellType = -1,
        });
        shot.Path.CopyFrom(world.Damage.LastPath);
        (shot.Terminal, shot.Effects, shot.Effect) = (world.Damage.LastTerminal, world.Damage.LastEffects, world.Damage.LastEffect);
        (shot.Steps, shot.StrikeMs, shot.FuzeT, shot.Fuze) = (trace.Steps.ToArray(), trace.StrikeMs, trace.FuzeT, trace.Fuze);
        for (int i = 0; i < state.Length; i++)
            if (k.State[i] != state[i] || k.Flags[i] != flags[i])
                shot.Parts.Add((i, state[i], k.State[i], flags[i], k.Flags[i]));
        for (int i = 0; i < breach.Length; i++)
        {
            if (k.BreachM2[i] > breach[i])
                shot.Holes.Add((i, k.BreachM2[i] - breach[i]));
        }
        // the fires it lit, at the cell each is shown at (a part's or a deck's: its section's top cell)
        for (int i = 0; i < fire.Length; i++)
            if (k.Fire[i].Burning && !fire[i] && !shot.Fires.Contains(k.Class.FireSpaces[i].Cell))
                shot.Fires.Add(k.Class.FireSpaces[i].Cell);
        return shot;
    }
}
