using Fleetwright.Sim.Core;

namespace Fleetwright.Sim;

/// <summary>What the world's own system (0) understands.</summary>
public enum WorldCommand : ushort
{
    SetSpeed = 1,
    Pause,
    Resume,
    SetRelation = 5,   // 4 was the map's scale, gone with the rails' one display rule (docs/battle/plan.md step 2.8e)
    SetPlayerSide,
    SetInterest,
    SetWeather,
}

/// <summary>The only way into the world (docs/world/architecture.md, "Commands in"): a player's or a captain's order,
/// plain data. <see cref="System"/> and <see cref="Code"/> say who handles it and what it means; 0 is the world
/// itself (<see cref="WorldCommand"/>). The world applies it at its current time and logs it with that stamp; the
/// start state, the settings and the log are a replay.</summary>
public readonly record struct Command(byte System, ushort Code, int Entity = 0, long A = 0, double X = 0, int B = 0, double Y = 0)
{
    /// <summary>Game seconds per real second, clamped to the settings' range; <paramref name="holdFine"/> keeps the
    /// fine tier whatever the speed (<see cref="World.FineHeld"/>); <paramref name="runBack"/> above 0 makes it a run,
    /// back to that speed at the first thing that matters (<see cref="World.RunBack"/>).</summary>
    public static Command SetSpeed(double speed, bool holdFine = false, double runBack = 0) =>
        new(0, (ushort)WorldCommand.SetSpeed, A: holdFine ? 1 : 0, X: speed, Y: runBack);
    public static Command Pause() => new(0, (ushort)WorldCommand.Pause);
    public static Command Resume() => new(0, (ushort)WorldCommand.Resume);

    /// <summary>The map's scale, metres per pixel (0: no map): the rails place what it can't show ship by ship more
    /// readily (ShipSystem.Tiers.cs). A host sends it when the scale changes, in steps, not every frame.
    /// <para>EXCEPTION to a hard rule: the sim is independent of display state, and this is the one display state it's
    /// told, kept only until the rails' snap can be hidden better. No other display state comes in.</para></summary>

    /// <summary>How two sides stand (<see cref="Relations"/>): war or peace, both ways.</summary>
    public static Command SetRelation(byte a, byte b, Relation r) =>
        new(0, (ushort)WorldCommand.SetRelation, a, A: (long)r, B: b);

    /// <summary>The side the player commands, or -1 for none (headless, or watching): whose encounters are the player's
    /// own (<see cref="Interest"/>), and later whose picture the snapshot is built from.</summary>
    public static Command SetPlayerSide(int side) => new(0, (ushort)WorldCommand.SetPlayerSide, side);

    /// <summary>The weather, the same everywhere (docs/battle/plan.md step 3.2b): the visibility, metres, and the wind,
    /// the way it blows from (degrees) and its speed (m/s), and the air's temperature over the sea's (K, to a mK).</summary>
    public static Command SetWeather(double visibilityM, double windFromDeg = 0, double windMs = 0, double airOverSeaK = 0) =>
        new(0, (ushort)WorldCommand.SetWeather, A: (long)Math.Round(windFromDeg * 1000), X: visibilityM, Y: windMs,
            B: (int)Math.Round(Math.Clamp(airOverSeaK, -50, 50) * 1000));

    /// <summary>What the clock does when an encounter of a kind opens (<see cref="Interest"/>).</summary>
    public static Command SetInterest(InterestKind kind, InterestAction action, double speed = 1) =>
        new(0, (ushort)WorldCommand.SetInterest, (int)kind, A: (long)action, X: speed);
}

/// <summary>A command as applied: the world time it took effect at.</summary>
public readonly record struct StampedCommand(WorldTime At, Command Command);
