using Fleetwright.Sim.Core;

namespace Fleetwright.Sim;

/// <summary>The only way out of the world (docs/world/architecture.md, "A snapshot out"): what the screens need,
/// copied after a step into a preallocated instance. Renderers read only this, never the world. Later steps add their
/// parts.</summary>
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

    /// <summary>Every ship at sea.</summary>
    public ShipsView Ships { get; } = new();
}

/// <summary>The ships' part of a snapshot: per live ship, in columns of which the first <see cref="Count"/> hold. A
/// pose is at its tick (<see cref="PoseMs"/>); a renderer carries it on to its own time along the arc of its speed and
/// rate of turn (<see cref="Combat.ShipMotion.Extrapolate"/>).</summary>
public sealed class ShipsView
{
    public int Count;
    public int[] Entity = [], Class = [];
    public Vec3d[] Position = [], Forward = [];
    /// <summary>m/s, negative astern; rad/s, positive to starboard; rad.</summary>
    public double[] Speed = [], YawRate = [], Rudder = [];
    public long[] PoseMs = [];
    public Combat.Helm[] Helm = [];
    public double[] CourseOrderDeg = [], SpeedOrderKn = [];

    /// <summary>Room for n ships; grows (allocates) only past the largest count yet.</summary>
    public void EnsureCapacity(int n)
    {
        if (n <= Entity.Length)
            return;
        n = Math.Max(n, Math.Max(16, Entity.Length * 2));
        Array.Resize(ref Entity, n);
        Array.Resize(ref Class, n);
        Array.Resize(ref Position, n);
        Array.Resize(ref Forward, n);
        Array.Resize(ref Speed, n);
        Array.Resize(ref YawRate, n);
        Array.Resize(ref Rudder, n);
        Array.Resize(ref PoseMs, n);
        Array.Resize(ref Helm, n);
        Array.Resize(ref CourseOrderDeg, n);
        Array.Resize(ref SpeedOrderKn, n);
    }

    /// <summary>Ship k's position and forward carried on to a time, ms.</summary>
    public (Vec3d Position, Vec3d Forward) PoseAt(int k, long timeMs)
    {
        var p = Position[k];
        var f = Forward[k];
        Combat.ShipMotion.Extrapolate(ref p, ref f, Speed[k], YawRate[k], (timeMs - PoseMs[k]) / 1000.0);
        return (p, f);
    }
}
