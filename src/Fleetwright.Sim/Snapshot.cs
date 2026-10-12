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

    /// <summary>Every group.</summary>
    public GroupsView Groups { get; } = new();
}

/// <summary>The groups' part of a snapshot: per group its id, name, formation and spacing, and its members (entities,
/// the guide first) as a run of <see cref="Members"/>.</summary>
public sealed class GroupsView
{
    public int Count;
    public int[] Id = [], MemberStart = [], MemberCount = [], Members = [];
    public string[] Name = [];
    public Formation[] Formation = [];
    public double[] SpacingM = [];

    /// <summary>Room for n groups and m members; grows only past the most yet.</summary>
    public void EnsureCapacity(int n, int m)
    {
        if (n > Id.Length)
        {
            n = Math.Max(n, Math.Max(8, Id.Length * 2));
            Array.Resize(ref Id, n);
            Array.Resize(ref MemberStart, n);
            Array.Resize(ref MemberCount, n);
            Array.Resize(ref Name, n);
            Array.Resize(ref Formation, n);
            Array.Resize(ref SpacingM, n);
        }
        if (m > Members.Length)
            Array.Resize(ref Members, Math.Max(m, Math.Max(32, Members.Length * 2)));
    }

    /// <summary>The index of the group a ship is in, or -1.</summary>
    public int IndexOfShip(int entity)
    {
        for (int g = 0; g < Count; g++)
            for (int i = 0; i < MemberCount[g]; i++)
                if (Members[MemberStart[g] + i] == entity)
                    return g;
        return -1;
    }
}

/// <summary>The ships' part of a snapshot: per live ship, in columns of which the first <see cref="Count"/> hold. A
/// pose is at its tick (<see cref="PoseMs"/>); a renderer carries it on to its own time along the arc of its speed and
/// rate of turn (<see cref="Combat.ShipMotion.Extrapolate"/>).</summary>
public sealed class ShipsView
{
    public int Count;
    public int[] Entity = [], Class = [];

    /// <summary>The side each ship is on (ships of different sides are opposing).</summary>
    public byte[] Side = [];
    public Vec3d[] Position = [], Forward = [];
    /// <summary>m/s, negative astern; rad/s, positive to starboard; rad.</summary>
    public double[] Speed = [], YawRate = [], Rudder = [];
    public long[] PoseMs = [];
    public Combat.Helm[] Helm = [];

    /// <summary>On the rails clock tier (a function of time; its pose is now's and carries on exactly).</summary>
    public bool[] OnRails = [];
    public double[] CourseOrderDeg = [], SpeedOrderKn = [];

    /// <summary>Who commands each ship and how, filled by the AI (0: nobody, the player's own orders stand); the
    /// meaning of the order is the AI's (<c>Fleetwright.Sim.Ai.CaptainOrder</c>), with its target ship's entity or 0.</summary>
    public byte[] Captain = [];
    public int[] CaptainTarget = [];

    /// <summary>Each ship's route still to steam: <see cref="RouteCount"/> waypoints from <see cref="RouteStart"/> in
    /// <see cref="Waypoints"/> (the next one first).</summary>
    public int[] RouteStart = [], RouteCount = [];

    /// <summary>The route goes round again after its last waypoint (a patrol): its waypoints are all listed, the next first.</summary>
    public bool[] RouteLoop = [];
    public Vec3d[] Waypoints = [];
    public int WaypointCount;

    /// <summary>Room for n waypoints in all; grows only past the most yet.</summary>
    public void EnsureWaypoints(int n)
    {
        if (n > Waypoints.Length)
            Array.Resize(ref Waypoints, Math.Max(n, Math.Max(64, Waypoints.Length * 2)));
    }

    /// <summary>The index of an entity's ship in these columns, or -1.</summary>
    public int IndexOf(int entity)
    {
        for (int k = 0; k < Count; k++)
            if (Entity[k] == entity)
                return k;
        return -1;
    }

    /// <summary>Room for n ships; grows (allocates) only past the largest count yet.</summary>
    public void EnsureCapacity(int n)
    {
        if (n <= Entity.Length)
            return;
        n = Math.Max(n, Math.Max(16, Entity.Length * 2));
        Array.Resize(ref Entity, n);
        Array.Resize(ref Class, n);
        Array.Resize(ref Side, n);
        Array.Resize(ref Position, n);
        Array.Resize(ref Forward, n);
        Array.Resize(ref Speed, n);
        Array.Resize(ref YawRate, n);
        Array.Resize(ref Rudder, n);
        Array.Resize(ref PoseMs, n);
        Array.Resize(ref Helm, n);
        Array.Resize(ref OnRails, n);
        Array.Resize(ref CourseOrderDeg, n);
        Array.Resize(ref SpeedOrderKn, n);
        Array.Resize(ref Captain, n);
        Array.Resize(ref CaptainTarget, n);
        Array.Resize(ref RouteStart, n);
        Array.Resize(ref RouteCount, n);
        Array.Resize(ref RouteLoop, n);
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
