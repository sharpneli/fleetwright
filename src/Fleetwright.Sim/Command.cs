using Fleetwright.Sim.Core;

namespace Fleetwright.Sim;

/// <summary>What the world's own system (0) understands.</summary>
public enum WorldCommand : ushort
{
    SetSpeed = 1,
    Pause,
    Resume,
    SetViewScale,
}

/// <summary>The only way into the world (docs/world/architecture.md, "Commands in"): a player's or a captain's order,
/// plain data. <see cref="System"/> and <see cref="Code"/> say who handles it and what it means; 0 is the world
/// itself (<see cref="WorldCommand"/>). The world applies it at its current time and logs it with that stamp; the
/// start state, the settings and the log are a replay.</summary>
public readonly record struct Command(byte System, ushort Code, int Entity = 0, long A = 0, double X = 0, int B = 0, double Y = 0)
{
    /// <summary>Game seconds per real second, clamped to the settings' range.</summary>
    public static Command SetSpeed(double speed) => new(0, (ushort)WorldCommand.SetSpeed, X: speed);
    public static Command Pause() => new(0, (ushort)WorldCommand.Pause);
    public static Command Resume() => new(0, (ushort)WorldCommand.Resume);

    /// <summary>The map's scale, metres per pixel (0: no map): the rails place what it can't show ship by ship more
    /// readily (ShipSystem.Rails.cs). A host sends it when the scale changes, in steps, not every frame.</summary>
    public static Command SetViewScale(double metresPerPixel) => new(0, (ushort)WorldCommand.SetViewScale, X: metresPerPixel);
}

/// <summary>A command as applied: the world time it took effect at.</summary>
public readonly record struct StampedCommand(WorldTime At, Command Command);
