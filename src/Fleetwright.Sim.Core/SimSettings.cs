namespace Fleetwright.Sim.Core;

/// <summary>Every tuning number of the sim, in one place (docs/world/architecture.md, "No magic values"). Immutable:
/// a world is made with one and keeps it, and a replay records it, since settings change results. The defaults here
/// are the one source until loading from data comes in step 2.2.</summary>
public sealed record SimSettings
{
    public static readonly SimSettings Default = new();

    /// <summary>The fastest the clock may be asked to run, game seconds per real second.</summary>
    public double MaxSpeed { get; init; } = 30_000;

    /// <summary>The slowest, above zero (zero is a pause).</summary>
    public double MinSpeed { get; init; } = 0.1;

    /// <summary>How far ahead of the clock the target may run, in real seconds at the asked speed. A sim slower than
    /// asked drops its achieved speed instead of building a backlog to catch up on later.</summary>
    public double MaxLagRealSeconds { get; init; } = 0.25;

    /// <summary>How often the sim thread publishes a snapshot while it's behind, wall milliseconds.</summary>
    public double PublishIntervalMs { get; init; } = 8;

    /// <summary>Events the sim thread runs between looks at the wall clock and the stop flag.</summary>
    public int EventsPerSlice { get; init; } = 4096;

    /// <summary>Room made up front, so play doesn't grow it: the event queue, the command queue and the command log.</summary>
    public int EventCapacity { get; init; } = 16_384;
    public int CommandCapacity { get; init; } = 256;
    public int CommandLogCapacity { get; init; } = 16_384;
}
