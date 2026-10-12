using Fleetwright.Sim.Core;

namespace Fleetwright.Sim;

/// <summary>The only way out of the world (docs/world/architecture.md, "A snapshot out"): what the screens need,
/// copied after a step into a preallocated instance. Renderers read only this, never the world. Later steps add their
/// parts (ships' poses, both the last tick's and this one's, so one snapshot is enough to interpolate).</summary>
public sealed class Snapshot
{
    /// <summary>Counts the snapshots a host has published.</summary>
    public long Sequence { get; set; }

    public WorldTime Time { get; set; }

    /// <summary>The time the world was advancing toward.</summary>
    public WorldTime Target { get; set; }

    /// <summary>The unit vector toward the sun at <see cref="Time"/>, Earth-fixed.</summary>
    public Vec3d Sun { get; set; }

    /// <summary>The asked speed, game seconds per real second.</summary>
    public double Speed { get; set; }

    public bool Paused { get; set; }

    /// <summary>The speed the clock actually kept over the last moments, measured by the host.</summary>
    public double AchievedSpeed { get; set; }

    /// <summary>Bytes the sim thread has allocated since it started: flat in play.</summary>
    public long SimAllocatedBytes { get; set; }

    /// <summary>What stopped the sim, if it crashed; it no longer advances.</summary>
    public string? Error { get; set; }
}
