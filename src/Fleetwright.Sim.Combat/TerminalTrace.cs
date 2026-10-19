using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>What the terminal walk made of a plate (<see cref="TerminalTrace"/>).</summary>
public enum PlateCall : byte
{
    /// <summary>Whole and through, slowed (and turned toward its normal).</summary>
    Through,
    /// <summary>Glanced off it, skidding along its face.</summary>
    Glanced,
    /// <summary>Holed it in pieces, under its Navy limit (P4).</summary>
    Holed,
    /// <summary>Stopped by it.</summary>
    Stopped,
    /// <summary>Shattered on its face-hardened face, and through it in pieces.</summary>
    Shattered,
    /// <summary>Through it just over its limit, the body breaking up.</summary>
    BrokeUp,
    /// <summary>The filler burst while getting through it.</summary>
    BurstInPlate,
    /// <summary>A nose fuze fired on it: punched through, bursting just inside.</summary>
    NoseInside,
    /// <summary>A nose fuze fired on it: too thick to punch, bursting outside.</summary>
    NoseOutside,
    /// <summary>Not a plate: a run through a medium inside a struck object (a boiler's tubes and water, a turbine's
    /// blading), its length <see cref="PlateStep.RunM"/>, slowing the shell (C1c).</summary>
    Medium,
}

/// <summary>What started a base fuze, or fired a nose fuze (<see cref="TerminalTrace"/>).</summary>
public enum FuzeCause : byte
{
    None,
    Plate,
    Water,
    /// <summary>A solid struck inside a space (docs/battle/shell-contents.md C1c).</summary>
    Contents,
    Nose,
}

/// <summary>One plate on the walk: its crossing on the path (and where, how far along), its thickness and the plate as
/// this shell met it (mm of the formula's plate: material, body, shatter), the obliquity, the shell's speed in, the
/// plate's limit as it met it (its lot and this shot's scatter in it, <see cref="Quality"/>), what it made of it, the
/// speed out and the turn toward its normal; whether the cap came off on it; and the delay its fuze had left leaving the
/// plate, seconds (C0: it runs on time; NaN when not running).</summary>
public struct PlateStep
{
    public int Crossing;
    public double T;
    public P3 Point;
    public double Mm, EffectiveMm, ObliquityDeg;
    public double InMs, LimitMs, Quality, OutMs, TurnDeg;
    public double FuzeLeftS;
    /// <summary>A struck solid's name (its skin, its core or its interior's run; null for a ship's plate), and a medium
    /// run's length, m.</summary>
    public string? Name;
    public double RunM;
    public PlateCall Call;
    public bool Decapped;
}

/// <summary>The terminal walk told plate by plate (a tool's look: the hitbox viewer's shot, benches): filled by
/// <see cref="Terminal.Resolve"/> when it's given one, the same walk the hit takes. Not world state; reused shot after
/// shot, it allocates nothing once made.</summary>
public sealed class TerminalTrace
{
    const int Capacity = 64;
    readonly PlateStep[] steps = new PlateStep[Capacity];
    int count;

    /// <summary>The plates, in the order the walk met them.</summary>
    public ReadOnlySpan<PlateStep> Steps => steps.AsSpan(0, count);

    /// <summary>More plates than it holds: the last ones were dropped.</summary>
    public bool Overflowed { get; private set; }

    /// <summary>The shell's speed at the hull, after any run under the water, m/s.</summary>
    public double StrikeMs { get; private set; }

    /// <summary>Where its fuze started (or a nose fuze fired): how far along the path, the crossing, and what did it
    /// (None: it never did).</summary>
    public double FuzeT { get; private set; } = double.NaN;
    public int FuzeCrossing { get; private set; } = -1;
    public FuzeCause Fuze { get; private set; }

    internal void Begin(double strikeMs)
    {
        (count, Overflowed, StrikeMs, FuzeT, FuzeCrossing, Fuze) = (0, false, strikeMs, double.NaN, -1, FuzeCause.None);
    }

    internal void Add(in PlateStep s)
    {
        if (count == Capacity)
        {
            Overflowed = true;
            return;
        }
        steps[count++] = s;
    }

    /// <summary>The last plate's fuze delay left, once the walk is past it.</summary>
    internal void FuzeLeft(double s)
    {
        if (count > 0)
            steps[count - 1].FuzeLeftS = s;
    }

    internal void Fuzed(double t, int crossing, FuzeCause cause) => (FuzeT, FuzeCrossing, Fuze) = (t, crossing, cause);
}
