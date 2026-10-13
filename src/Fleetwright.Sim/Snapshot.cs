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

    /// <summary>The sim thread's probes as they stood at this snapshot (docs/battle/plan.md step 2.7b): sums since the
    /// world began, longest calls since the snapshot before.</summary>
    public Probes Probes { get; } = new();

    /// <summary>When it was published, <see cref="Probes.Now"/>: how old it is when a frame draws it.</summary>
    public long PublishedAt { get; set; }

    /// <summary>What stopped the sim, if it crashed; it no longer advances.</summary>
    public string? Error { get; set; }

    /// <summary>Every ship at sea.</summary>
    public ShipsView Ships { get; } = new();

    /// <summary>Every group.</summary>
    public GroupsView Groups { get; } = new();

    /// <summary>The side the player commands, -1 for none.</summary>
    public int PlayerSide { get; set; } = -1;

    /// <summary>Filled with the truth (a dev's view, the host's choice): every ship, whoever knows of it. Otherwise the
    /// player's view (docs/world/architecture.md "Knowledge"): their own ships and those of sides not at war with them,
    /// and the enemy only as <see cref="Contacts"/> from their navy's picture.</summary>
    public bool Truth { get; set; }

    /// <summary>The sides whose ships (and groups) are in this snapshot, by side.</summary>
    public bool[] ShownSides { get; } = new bool[256];

    /// <summary>The player's navy's contacts (empty with no player side).</summary>
    public ContactsView Contacts { get; } = new();

    /// <summary>The enemy ships the player's navy has in sight, as they are (their contacts' ships, for the map to draw
    /// whole): pose, class and mounts, no orders or route, the side as the contact has it (255 until its type is made
    /// out). Empty with the truth, which has them in <see cref="Ships"/>.</summary>
    public ShipsView Sighted { get; } = new();

    /// <summary>The weather (one for the world for now).</summary>
    public WeatherHere Weather { get; set; }

    /// <summary>The player's rules for what the clock does when an encounter opens (<see cref="Interest"/>).</summary>
    public InterestRule InterestOwn { get; set; }
    public InterestRule InterestOthers { get; set; }

    /// <summary>The open encounters, and the last that was news to the player.</summary>
    public EncountersView Encounters { get; } = new();

    /// <summary>Shells in flight (each drawn from its ballistic table at the frame's time) and the splashes and hits of
    /// the last <see cref="SimSettings.SplashLifeMs"/> (docs/battle/fighting.md 4.2a).</summary>
    public ShellsView Shells { get; } = new();
    public SplashesView Splashes { get; } = new();

    /// <summary>The engaged batteries of the ships shown, and the newest lines of the combat log (docs/battle/fighting.md
    /// 4.2b).</summary>
    public BatteriesView Batteries { get; } = new();
    public CombatLogView CombatLog { get; } = new();
}

/// <summary>An engaged battery: its ship (an entity) and battery (the class's index), what it's engaged on (an entity),
/// what it's doing, its level (local control at <see cref="Local"/>), and its salvos, shells and hits so far.</summary>
public sealed class BatteriesView
{
    public int Count;
    public int[] Shooter = [], Battery = [], Target = [], Salvos = [], Shells = [], Hits = [];
    public BatteryState[] State = [];
    public byte[] Level = [];
    public bool[] Local = [];

    public void EnsureCapacity(int n)
    {
        if (n <= Shooter.Length)
            return;
        n = Math.Max(n, Math.Max(16, Shooter.Length * 2));
        Array.Resize(ref Shooter, n);
        Array.Resize(ref Battery, n);
        Array.Resize(ref Target, n);
        Array.Resize(ref Salvos, n);
        Array.Resize(ref Shells, n);
        Array.Resize(ref Hits, n);
        Array.Resize(ref State, n);
        Array.Resize(ref Level, n);
        Array.Resize(ref Local, n);
    }
}

/// <summary>The combat log's newest lines (<see cref="Sim.CombatLog"/>), newest first, and how many were ever written.</summary>
public sealed class CombatLogView
{
    public int Count;
    public long Written;
    public CombatEntry[] Entries = [];

    public void EnsureCapacity(int n)
    {
        if (n > Entries.Length)
            Array.Resize(ref Entries, Math.Max(n, 16));
    }
}

/// <summary>The shells in flight: per shell its ballistic table (an index into <see cref="Ballistics"/>, which the
/// screens may read from their thread), when it was fired, its muzzle and heading, the elevation and muzzle velocity
/// (a fraction of the shell's own) it left with, when its flight ends (seconds after firing), the ship it hits (an
/// entity, -1 for the water) and who fired it. Its position at any moment is
/// <c>Ballistics[Table].Position(Muzzle, Forward, ElevationDeg, Speed, t)</c>.</summary>
public sealed class ShellsView
{
    public int Count;
    public int[] Table = [], Hit = [], Shooter = [];
    public long[] FiredMs = [];
    public Vec3d[] Muzzle = [], Forward = [];
    public double[] ElevationDeg = [], Speed = [], EndS = [];
    public Combat.BallisticsRegistry? Ballistics;

    public void EnsureCapacity(int n)
    {
        if (n <= Table.Length)
            return;
        n = Math.Max(n, Math.Max(16, Table.Length * 2));
        Array.Resize(ref Table, n);
        Array.Resize(ref Hit, n);
        Array.Resize(ref Shooter, n);
        Array.Resize(ref FiredMs, n);
        Array.Resize(ref Muzzle, n);
        Array.Resize(ref Forward, n);
        Array.Resize(ref ElevationDeg, n);
        Array.Resize(ref Speed, n);
        Array.Resize(ref EndS, n);
    }
}

/// <summary>The splashes and hits of the last while, newest first: where, when, the ship hit (an entity, -1 for a
/// splash) and who fired.</summary>
public sealed class SplashesView
{
    public int Count;
    public Vec3d[] Point = [];
    public long[] Ms = [];
    public int[] Ship = [], Shooter = [];

    public void EnsureCapacity(int n)
    {
        if (n <= Point.Length)
            return;
        n = Math.Max(n, Math.Max(16, Point.Length * 2));
        Array.Resize(ref Point, n);
        Array.Resize(ref Ms, n);
        Array.Resize(ref Ship, n);
        Array.Resize(ref Shooter, n);
    }
}

/// <summary>The player's navy's picture, as the map draws it (docs/battle/plan.md step 3.2e): per contact the ship it's
/// on (the key: contacts are on true ships in the battle module), the estimate carried on to the snapshot's time (position
/// and velocity) and its error ellipse (1-sigma semi-axes, the major's bearing), whether it's in sight and when it was
/// last seen, how far it's made out, its type, the class it's taken for (a registry id, -1 below class), and its side once
/// its type is made out (255 before).</summary>
public sealed class ContactsView
{
    public int Count;
    public int[] Entity = [], TakenFor = [];
    public Vec3d[] Position = [], Velocity = [];
    public double[] MajorM = [], MinorM = [], AxisDeg = [];
    public bool[] InSight = [];
    public long[] SeenMs = [];
    public ContactLevel[] Level = [];
    public ShipType[] Type = [];
    public byte[] Side = [];

    public void EnsureCapacity(int n)
    {
        if (n <= Entity.Length)
            return;
        n = Math.Max(n, Math.Max(16, Entity.Length * 2));
        Array.Resize(ref Entity, n);
        Array.Resize(ref TakenFor, n);
        Array.Resize(ref Position, n);
        Array.Resize(ref Velocity, n);
        Array.Resize(ref MajorM, n);
        Array.Resize(ref MinorM, n);
        Array.Resize(ref AxisDeg, n);
        Array.Resize(ref InSight, n);
        Array.Resize(ref SeenMs, n);
        Array.Resize(ref Level, n);
        Array.Resize(ref Type, n);
        Array.Resize(ref Side, n);
    }
}

/// <summary>The encounters' part of a snapshot: per open encounter (by id, oldest first) where it is, how far its ships
/// reach, how many, when it opened and whether the player's ships are in it; and the last encounter that was news to the
/// player (opened, or the player's ships came into it), with when.</summary>
public sealed class EncountersView
{
    public int Count;
    public int[] Id = [], Ships = [];
    public Vec3d[] Centre = [];
    public double[] RadiusM = [];
    public long[] OpenedMs = [];
    public bool[] Own = [];

    /// <summary>The last encounter news (its id, 0 for none yet), its kind and when.</summary>
    public int NewsId;
    public InterestKind NewsKind;
    public long NewsMs;

    public void EnsureCapacity(int n)
    {
        if (n <= Id.Length)
            return;
        n = Math.Max(n, Math.Max(8, Id.Length * 2));
        Array.Resize(ref Id, n);
        Array.Resize(ref Ships, n);
        Array.Resize(ref Centre, n);
        Array.Resize(ref RadiusM, n);
        Array.Resize(ref OpenedMs, n);
        Array.Resize(ref Own, n);
    }
}

/// <summary>The groups' part of a snapshot: per group its id, name, formation, spacing, whether it's a force (its
/// members are groups, not ships), the force it's in (0 for none), its guide (the first member in its line, 0 when every
/// member is detached) and the ship that leads it, and its members (entities or group ids, in order) as a run of
/// <see cref="Members"/>, each with its <see cref="Detached"/>.</summary>
public sealed class GroupsView
{
    public int Count;
    public int[] Id = [], MemberStart = [], MemberCount = [], Members = [], Guide = [], GuideShip = [], Parent = [];
    public bool[] Force = [];

    /// <summary>Held by the player while its superior's orders for it have changed: a notice, "resume?" (the AI's).</summary>
    public bool[] Asked = [];
    public Detachment[] Detached = [];
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
            Array.Resize(ref Guide, n);
            Array.Resize(ref GuideShip, n);
            Array.Resize(ref Parent, n);
            Array.Resize(ref Force, n);
            Array.Resize(ref Asked, n);
        }
        if (m > Members.Length)
        {
            m = Math.Max(m, Math.Max(32, Members.Length * 2));
            Array.Resize(ref Members, m);
            Array.Resize(ref Detached, m);
        }
    }

    /// <summary>The index of the squadron a ship is in, or -1.</summary>
    public int IndexOfShip(int entity)
    {
        for (int g = 0; g < Count; g++)
            if (!Force[g])
                for (int i = 0; i < MemberCount[g]; i++)
                    if (Members[MemberStart[g] + i] == entity)
                        return g;
        return -1;
    }

    /// <summary>The index of a group by its id, or -1.</summary>
    public int IndexOfGroup(int groupId)
    {
        for (int g = 0; g < Count; g++)
            if (Id[g] == groupId)
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

    /// <summary>The encounter each ship is in, 0 for none.</summary>
    public int[] Encounter = [];
    public Vec3d[] Position = [], Forward = [];
    /// <summary>m/s, negative astern; rad/s, positive to starboard; rad.</summary>
    public double[] Speed = [], YawRate = [], Rudder = [];
    public long[] PoseMs = [];
    public Combat.Helm[] Helm = [];

    /// <summary>Its clock tier: on rails or coarse its pose is now's, a function of time; fine, its tick's.</summary>
    public Combat.ClockTier[] Tier = [];

    /// <summary>When a coarse ship's turn (its segment) ends, ms: carried on past it, it goes straight (long.MaxValue
    /// for none: fine, on rails, riding).</summary>
    public long[] TurnEndMs = [];
    public double[] CourseOrderDeg = [], SpeedOrderKn = [];

    /// <summary>Who commands each ship and how, filled by the AI (0: nobody, the player's own orders stand); the
    /// meaning of the order is the AI's (<c>Fleetwright.Sim.Ai.CaptainOrder</c>), with its target ship's entity or 0.</summary>
    public byte[] Captain = [];
    public int[] CaptainTarget = [];

    /// <summary>Each ship's fire order (the AI's <c>Fleetwright.Sim.Ai.FireOrder</c>: at will, hold, engage) and the
    /// ship it engages (0: none).</summary>
    public byte[] Fire = [];
    public int[] FireTarget = [];

    /// <summary>Held by the player while its superior's orders for it have changed: a notice, "resume?" (the AI's).</summary>
    public bool[] Asked = [];

    /// <summary>Ships and the land (<see cref="Combat.Slide"/>): sliding along a coast (+1 land to port, -1 to
    /// starboard, 0 not), how often the backstop has put it back off the land; its minimum and standoff from the
    /// coast and the feeler's look-ahead, metres (the map's overlay).</summary>
    public sbyte[] LandSide = [];
    public int[] Backstops = [];
    public double[] MinClearanceM = [], StandoffM = [], LookaheadM = [];

    /// <summary>Each ship's route still to steam: <see cref="RouteCount"/> waypoints from <see cref="RouteStart"/> in
    /// <see cref="Waypoints"/> (the next one first).</summary>
    public int[] RouteStart = [], RouteCount = [];

    /// <summary>The route goes round again after its last waypoint (a patrol): its waypoints are all listed, the next first.</summary>
    public bool[] RouteLoop = [];
    public Vec3d[] Waypoints = [];
    public int WaypointCount;

    /// <summary>Each ship's mounts' bearings now, degrees clockwise from ahead, from <see cref="MountStart"/> in
    /// <see cref="MountBearingDeg"/>, a mount per the class's mounts in order (docs/battle/fighting.md 4.2b); -1 for a
    /// ship whose mounts have never left their rest (draw them at it).</summary>
    public int[] MountStart = [];
    public double[] MountBearingDeg = [];
    public int MountCount;

    /// <summary>Room for n mounts' bearings in all; grows only past the most yet.</summary>
    public void EnsureMounts(int n)
    {
        if (n > MountBearingDeg.Length)
            Array.Resize(ref MountBearingDeg, Math.Max(n, Math.Max(64, MountBearingDeg.Length * 2)));
    }

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
        Array.Resize(ref Encounter, n);
        Array.Resize(ref Position, n);
        Array.Resize(ref Forward, n);
        Array.Resize(ref Speed, n);
        Array.Resize(ref YawRate, n);
        Array.Resize(ref Rudder, n);
        Array.Resize(ref PoseMs, n);
        Array.Resize(ref Helm, n);
        Array.Resize(ref Tier, n);
        Array.Resize(ref TurnEndMs, n);
        Array.Resize(ref CourseOrderDeg, n);
        Array.Resize(ref SpeedOrderKn, n);
        Array.Resize(ref Captain, n);
        Array.Resize(ref CaptainTarget, n);
        Array.Resize(ref Fire, n);
        Array.Resize(ref FireTarget, n);
        Array.Resize(ref Asked, n);
        Array.Resize(ref LandSide, n);
        Array.Resize(ref Backstops, n);
        Array.Resize(ref MinClearanceM, n);
        Array.Resize(ref StandoffM, n);
        Array.Resize(ref LookaheadM, n);
        Array.Resize(ref RouteStart, n);
        Array.Resize(ref RouteCount, n);
        Array.Resize(ref RouteLoop, n);
        int had = MountStart.Length;
        Array.Resize(ref MountStart, n);
        Array.Fill(MountStart, -1, had, n - had);   // at rest, unless filled
    }

    /// <summary>Ship k's position and forward carried on to a time, ms: along its arc, and straight on past its turn's end.</summary>
    public (Vec3d Position, Vec3d Forward) PoseAt(int k, long timeMs)
    {
        var p = Position[k];
        var f = Forward[k];
        double dt = (timeMs - PoseMs[k]) / 1000.0, turning = dt;
        if (TurnEndMs[k] != long.MaxValue && dt > 0)
            turning = Math.Clamp((TurnEndMs[k] - PoseMs[k]) / 1000.0, 0, dt);
        Combat.ShipMotion.Extrapolate(ref p, ref f, Speed[k], YawRate[k], turning);
        if (dt > turning)
            Combat.ShipMotion.Extrapolate(ref p, ref f, Speed[k], 0, dt - turning);
        return (p, f);
    }
}
