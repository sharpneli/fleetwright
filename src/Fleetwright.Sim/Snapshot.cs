using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

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

    /// <summary>A tier held whatever the speed (World.Held): fine, the clock as fast as the sim can with the world
    /// 240×'s; coarse, the step mode's, the world 241×'s at a watching speed.</summary>
    public ClockHold Held { get; set; }

    public bool FineHeld => Held == ClockHold.Fine;
    public bool CoarseHeld => Held == ClockHold.Coarse;

    /// <summary>The coarse step boundary a step runs to (World.StepEndMs), -1 with none.</summary>
    public long StepEndMs { get; set; } = -1;

    /// <summary>A run's speed to drop back to (World.RunBack), 0 with none.</summary>
    public double RunBack { get; set; }

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

    /// <summary>The smoke field's copies for the drawing (docs/battle/smoke.md Z5), taken with their own
    /// <see cref="TripleBuffer{T}.Acquire"/> (a copy outlives the snapshots); null while the field is off.</summary>
    public TripleBuffer<SmokeCopy>? SmokeCopies { get; set; }

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

    /// <summary>Each side's commander (docs/battle/command.md 5.1d), by side: his mode (the AI's <c>CommanderMode</c>, 0 for
    /// none), whether he's commanding now, and how he sees the battle (<c>CommanderPhase</c>) and why
    /// (<c>CommanderReason</c>).</summary>
    public byte[] CommanderMode { get; } = new byte[256];
    public bool[] CommanderOn { get; } = new bool[256];
    public byte[] CommanderPhase { get; } = new byte[256];
    public byte[] CommanderReason { get; } = new byte[256];

    /// <summary>Each side's doctrine's aggression as set, -1 to +1 (the AI's; 0 unset).</summary>
    public double[] Aggression { get; } = new double[256];

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

    /// <summary>The mounts fired in the last <see cref="SimSettings.SplashLifeMs"/>, newest first
    /// (docs/visuals/muzzle-flashes.md F0).</summary>
    public ShotsView Shots { get; } = new();

    /// <summary>The magazines set off in the last <see cref="SimSettings.BlastLifeMs"/>, newest first
    /// (docs/visuals/magazine-explosions.md X0).</summary>
    public BlastsView Blasts { get; } = new();

    /// <summary>The shells that struck a ship in the last <see cref="SimSettings.SplashLifeMs"/>, newest first, with what
    /// the world view draws of each (docs/visuals/hits-and-fires.md I0).</summary>
    public HitsView Hits { get; } = new();

    /// <summary>Torpedoes running (each where its run puts it at the frame's time) and their ends of the last
    /// <see cref="SimSettings.SplashLifeMs"/>: hits, and those that sank at the end of their run (docs/battle/fighting.md
    /// 4.6).</summary>
    public TorpedoesView Torpedoes { get; } = new();
    public TorpedoEndsView TorpedoEnds { get; } = new();

    /// <summary>The searchlights' beams out at the last look (docs/battle/night.md Q4).</summary>
    public BeamsView Beams { get; } = new();

    /// <summary>The star shells' flares burning (docs/battle/night.md Q5).</summary>
    public FlaresView Flares { get; } = new();

    /// <summary>The engaged batteries of the ships shown, and the newest lines of the combat log (docs/battle/fighting.md
    /// 4.2b).</summary>
    public BatteriesView Batteries { get; } = new();
    public CombatLogView CombatLog { get; } = new();

    /// <summary>The condition of the shown ships that have been hit (docs/battle/fighting.md 4.3a).</summary>
    public ConditionsView Conditions { get; } = new();

    /// <summary>The most ships a snapshot details (<see cref="Details"/>): the screens' selection and pinned cards.</summary>
    public const int MaxDetail = 4;

    /// <summary>The ships the host asked to detail (entities; the first <see cref="DetailWantedCount"/>): the screens'
    /// choice, like <see cref="Truth"/>, never the world's state.</summary>
    public int[] DetailWanted { get; } = new int[MaxDetail];
    public int DetailWantedCount;

    /// <summary>The ship card's detail (docs/map-ui/battle-hud.md U3a) for the ships asked for that are shown: each
    /// mount's state and rounds, the crew, damage control's parties and pumps, the flooded spaces, the holes and the
    /// magazines flooded.</summary>
    public ShipDetailView Details { get; } = new();

    /// <summary>The scenario's end once its judge has called it (docs/map-ui/battle-hud.md U7; null until then, and in a
    /// world without a scenario): made once, the same object in every snapshot after.</summary>
    public EndView? End;
}

/// <summary>How a scenario ended (docs/map-ui/battle-hud.md U7): why and when, each side's count and each ship's fate,
/// the truth on both sides (decision 5: the battler's result is its score). Made once by the host's judge, never
/// changed.</summary>
public sealed class EndView
{
    /// <summary>Why: "decided", "time", "quiet" (no fight for a while), "done" (every side done).</summary>
    public required string Reason { get; init; }
    public long EndMs { get; init; }
    public required SideEnd[] Sides { get; init; }
    public required ShipEnd[] Ships { get; init; }
}

/// <summary>A side's count at the end: its ships and tonnage, those lost, damaged and afloat, whether its aims are met,
/// its hits and torpedoes fired.</summary>
public readonly record struct SideEnd(string Name, int Ships, double Tonnes, int Lost, double LostTonnes, int Damaged, int Afloat, bool AimsMet,
    int Hits, int Torpedoes);

/// <summary>A ship's fate: lost (how and when) or afloat (damaged or not), the hits she took.</summary>
public readonly record struct ShipEnd(int Entity, int Side, string Name, bool Lost, Combat.LossKind Loss, long LossMs, bool Damaged, int Hits);

/// <summary>A mount's state as a ship card shows it: ready, loading, jammed in train, down for a while (crew stunned,
/// power tripped), or out (wrecked, disabled, its magazine flooded or gone).</summary>
public enum MountStatus : byte
{
    Ready,
    Loading,
    Jammed,
    Down,
    Out,
}

/// <summary>The detail of a few of the shown ships (<see cref="Snapshot.DetailWanted"/>; docs/map-ui/battle-hud.md U3a),
/// a row a ship with runs into pools: per class mount its <see cref="MountStatus"/>, until when (world ms; 0 none,
/// long.MaxValue the battle), the guns out of it, and its rounds by kind (AP, common, HE; int.MaxValue uncounted); the
/// crew (complement, killed, wounded, out now); damage control's parties (men, job, the fire space fought or -1, where
/// the job is, man-minutes done, section) and the pumps rigged; the spaces with water (cell, share of its capacity,
/// counterflooded); the holes to the sea (cell, m², mean height and side, ship frame); the magazines flooded (rooms).</summary>
public sealed class ShipDetailView
{
    public int Count;
    public int[] Entity = new int[Snapshot.MaxDetail], PumpsRigged = new int[Snapshot.MaxDetail];
    public double[] Complement = new double[Snapshot.MaxDetail], Killed = new double[Snapshot.MaxDetail],
        Wounded = new double[Snapshot.MaxDetail], CrewOut = new double[Snapshot.MaxDetail];
    public int[] MountStart = new int[Snapshot.MaxDetail], MountCount = new int[Snapshot.MaxDetail],
        PartyStart = new int[Snapshot.MaxDetail], PartyCount = new int[Snapshot.MaxDetail],
        FloodStart = new int[Snapshot.MaxDetail], FloodCount = new int[Snapshot.MaxDetail],
        HoleStart = new int[Snapshot.MaxDetail], HoleCount = new int[Snapshot.MaxDetail],
        MagazineStart = new int[Snapshot.MaxDetail], MagazineCount = new int[Snapshot.MaxDetail];

    // the mounts' pool
    public int Mounts;
    public MountStatus[] Mount = [];
    public long[] MountUntilMs = [];
    public int[] MountGunsOut = [], MountAp = [], MountCommon = [], MountHe = [], MountStar = [];

    // the parties' pool
    public int Parties;
    public double[] PartyMen = [], PartyDone = [];
    public Fleetwright.Sim.Combat.RepairJob[] PartyJob = [];
    public int[] PartyFire = [], PartyAt = [], PartySection = [];

    // the flooded spaces', holes' and flooded magazines' pools
    public int Floods, Holes, Magazines;
    public int[] FloodCell = [], HoleCell = [], MagazineRoom = [];
    public double[] FloodShare = [], HoleM2 = [], HoleY = [], HoleZ = [];
    public bool[] FloodCounter = [];

    /// <summary>The row of a ship (an entity), or -1 (not detailed).</summary>
    public int IndexOf(int entity)
    {
        for (int i = 0; i < Count; i++)
            if (Entity[i] == entity)
                return i;
        return -1;
    }

    public void EnsureMounts(int n)
    {
        if (n <= Mount.Length)
            return;
        n = Math.Max(n, Math.Max(32, Mount.Length * 2));
        Array.Resize(ref Mount, n);
        Array.Resize(ref MountUntilMs, n);
        Array.Resize(ref MountGunsOut, n);
        Array.Resize(ref MountAp, n);
        Array.Resize(ref MountCommon, n);
        Array.Resize(ref MountHe, n);
        Array.Resize(ref MountStar, n);
    }

    public void EnsureParties(int n)
    {
        if (n <= PartyMen.Length)
            return;
        n = Math.Max(n, Math.Max(16, PartyMen.Length * 2));
        Array.Resize(ref PartyMen, n);
        Array.Resize(ref PartyDone, n);
        Array.Resize(ref PartyJob, n);
        Array.Resize(ref PartyFire, n);
        Array.Resize(ref PartyAt, n);
        Array.Resize(ref PartySection, n);
    }

    public void EnsureFloods(int n)
    {
        if (n <= FloodCell.Length)
            return;
        n = Math.Max(n, Math.Max(32, FloodCell.Length * 2));
        Array.Resize(ref FloodCell, n);
        Array.Resize(ref FloodShare, n);
        Array.Resize(ref FloodCounter, n);
    }

    public void EnsureHoles(int n)
    {
        if (n <= HoleCell.Length)
            return;
        n = Math.Max(n, Math.Max(32, HoleCell.Length * 2));
        Array.Resize(ref HoleCell, n);
        Array.Resize(ref HoleM2, n);
        Array.Resize(ref HoleY, n);
        Array.Resize(ref HoleZ, n);
    }

    public void EnsureMagazines(int n)
    {
        if (n > MagazineRoom.Length)
            Array.Resize(ref MagazineRoom, Math.Max(n, Math.Max(8, MagazineRoom.Length * 2)));
    }
}

/// <summary>A damaged ship's condition, as its own side knows it: hits taken, the share of its power left, its
/// steering, whether it's going down (and how), its mounts and directors working of all, crew lost, fires, the area of
/// its holes to the sea (m²), whether it's conned, the water aboard and what's left of its reserve buoyancy (t), and the
/// attitude it has settled to.</summary>
public sealed class ConditionsView
{
    public int Count;
    public int[] Entity = [], Hits = [], MountsWorking = [], Mounts = [], DirectorsWorking = [], Directors = [], Fires = [];
    public double[] PowerCap = [], CrewLost = [], BreachM2 = [], WaterT = [], ReserveT = [];
    /// <summary>The share of its power that drives it, its water's weight taken off (what its speed comes from).</summary>
    public double[] HeldCap = [];
    /// <summary>Damage control's (docs/battle/damage-control.md DC8): the speed it asks for (knots, infinite: none) and the
    /// counterflooding left on each side, t (the doom clock).</summary>
    public double[] SafeSpeedKn = [], CounterfloodPortT = [], CounterfloodStarboardT = [];
    public Fleetwright.Sim.Combat.Attitude[] Attitude = [];
    public Fleetwright.Sim.Combat.LossKind[] Loss = [];
    public Fleetwright.Sim.Combat.Steering[] Steering = [];
    public Fleetwright.Sim.Combat.ShipStatus[] Status = [];
    public bool[] Conned = [];

    /// <summary>The row of a ship (an entity), or -1 (not hit).</summary>
    public int IndexOf(int entity)
    {
        for (int i = 0; i < Count; i++)
            if (Entity[i] == entity)
                return i;
        return -1;
    }

    public void EnsureCapacity(int n)
    {
        if (n <= Entity.Length)
            return;
        n = Math.Max(n, Math.Max(16, Entity.Length * 2));
        Array.Resize(ref Entity, n);
        Array.Resize(ref Hits, n);
        Array.Resize(ref MountsWorking, n);
        Array.Resize(ref Mounts, n);
        Array.Resize(ref DirectorsWorking, n);
        Array.Resize(ref Directors, n);
        Array.Resize(ref Fires, n);
        Array.Resize(ref PowerCap, n);
        Array.Resize(ref CrewLost, n);
        Array.Resize(ref BreachM2, n);
        Array.Resize(ref WaterT, n);
        Array.Resize(ref HeldCap, n);
        Array.Resize(ref SafeSpeedKn, n);
        Array.Resize(ref CounterfloodPortT, n);
        Array.Resize(ref CounterfloodStarboardT, n);
        Array.Resize(ref ReserveT, n);
        Array.Resize(ref Attitude, n);
        Array.Resize(ref Loss, n);
        Array.Resize(ref Steering, n);
        Array.Resize(ref Status, n);
        Array.Resize(ref Conned, n);
    }
}

/// <summary>An engaged battery: its ship (an entity) and battery (the class's index), what it's engaged on (an entity),
/// what it's doing and where in its fire discipline, its level (local control at <see cref="Local"/>), and its salvos,
/// shells and hits so far; and for the ship card (docs/map-ui/battle-hud.md U3a) the range to its aim and the computer's
/// doubt along the line of fire (m), the straddles called, its salvos in the air, the misses in a row in fire for
/// effect, and whether it saw her on its last second.</summary>
public sealed class BatteriesView
{
    public int Count;
    public int[] Shooter = [], Battery = [], Target = [], Salvos = [], Shells = [], Hits = [];
    public BatteryState[] State = [];
    public FirePhase[] Phase = [];
    public byte[] Level = [];
    public bool[] Local = [];
    public double[] RangeM = [], SigmaM = [];
    public int[] Straddles = [], InFlight = [], Misses = [];
    public bool[] Sees = [];

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
        Array.Resize(ref Phase, n);
        Array.Resize(ref Level, n);
        Array.Resize(ref Local, n);
        Array.Resize(ref RangeM, n);
        Array.Resize(ref SigmaM, n);
        Array.Resize(ref Straddles, n);
        Array.Resize(ref InFlight, n);
        Array.Resize(ref Misses, n);
        Array.Resize(ref Sees, n);
    }
}

/// <summary>The combat log's newest lines the player may know (<see cref="Sim.CombatLog"/>; docs/map-ui/battle-hud.md
/// U4), newest first, each with its place in the whole log (<see cref="Seq"/>: 0 the first line ever written), and how
/// many were ever written. A line between ships of sides not shown is left out; one about a shown side's fire on a ship
/// not shown keeps only what her fire's watchers could see of it (no part, no outcome; a fire, her magazine, broken in
/// two).</summary>
public sealed class CombatLogView
{
    public int Count;
    public long Written;
    public CombatEntry[] Entries = [];
    public long[] Seq = [];

    public void EnsureCapacity(int n)
    {
        if (n > Entries.Length)
        {
            Array.Resize(ref Entries, Math.Max(n, 16));
            Array.Resize(ref Seq, Entries.Length);
        }
    }
}

/// <summary>The shells in flight: per shell its ballistic table (an index into <see cref="Ballistics"/>, which the
/// screens may read from their thread), when it was fired, its muzzle and heading, the elevation and muzzle velocity
/// (a fraction of the shell's own) it left with, when its flight ends (seconds after firing), the ship it hits (an
/// entity, -1 for the water) and who fired it. Its position at any moment is
/// <c>Ballistics[Table].Position(Muzzle, Forward, ElevationDeg, Speed, t)</c> until it meets the water at WaterS (its end,
/// unless it runs on under it to a hit); then straight on from Water to End, the hit's point.</summary>
public sealed class ShellsView
{
    public int Count;
    public int[] Table = [], Hit = [], Shooter = [];
    public long[] FiredMs = [];
    public Vec3d[] Muzzle = [], Forward = [], Water = [], End = [];
    public double[] ElevationDeg = [], Speed = [], WaterS = [], EndS = [];
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
        Array.Resize(ref WaterS, n);
        Array.Resize(ref Water, n);
        Array.Resize(ref End, n);
    }
}

/// <summary>The searchlights' beams out (docs/battle/night.md Q4): each one's lamp (its height in the point), the true
/// bearing it's on, whose (an entity) and its peak (cd).</summary>
public sealed class BeamsView
{
    public int Count;
    public Vec3d[] Lamp = [];
    public double[] BearingDeg = [], Candela = [];
    public int[] Ship = [];

    public void EnsureCapacity(int n)
    {
        if (n <= Lamp.Length)
            return;
        n = Math.Max(n, Math.Max(16, Lamp.Length * 2));
        Array.Resize(ref Lamp, n);
        Array.Resize(ref BearingDeg, n);
        Array.Resize(ref Candela, n);
        Array.Resize(ref Ship, n);
    }
}

/// <summary>The star shells' flares burning (docs/battle/night.md Q5): each one's place (its height in the point) and
/// light (cd).</summary>
public sealed class FlaresView
{
    public int Count;
    public Vec3d[] At = [];
    public double[] Candela = [];

    public void EnsureCapacity(int n)
    {
        if (n <= At.Length)
            return;
        n = Math.Max(n, Math.Max(16, At.Length * 2));
        Array.Resize(ref At, n);
        Array.Resize(ref Candela, n);
    }
}

/// <summary>Torpedoes running: where each entered the water (on the sea's surface) and its heading there, when, its
/// speed, m/s, how long its run lasts, s (to its end, or the hit), who fired it and the ship it'll hit (an entity, or -1);
/// for the world view (docs/visuals/torpedoes.md R0) its serial, the depth it runs at, m, and where it came from: the
/// shooter's class (an index, -1 unknown), its launcher and its torpedo (the class's rows).</summary>
public sealed class TorpedoesView
{
    public int Count;
    public Vec3d[] Origin = [], Forward = [];
    public long[] LaunchMs = [], Serial = [];
    public double[] SpeedMs = [], EndS = [], DepthM = [];
    public int[] Shooter = [], Hit = [], Class = [], Launcher = [], Row = [];

    public void EnsureCapacity(int n)
    {
        if (n <= Origin.Length)
            return;
        n = Math.Max(n, Math.Max(16, Origin.Length * 2));
        Array.Resize(ref Origin, n);
        Array.Resize(ref Forward, n);
        Array.Resize(ref LaunchMs, n);
        Array.Resize(ref SpeedMs, n);
        Array.Resize(ref EndS, n);
        Array.Resize(ref Shooter, n);
        Array.Resize(ref Hit, n);
        Array.Resize(ref Serial, n);
        Array.Resize(ref DepthM, n);
        Array.Resize(ref Class, n);
        Array.Resize(ref Launcher, n);
        Array.Resize(ref Row, n);
    }

    /// <summary>Where torpedo i is at a moment (on the surface over it), and how far it has run, m.</summary>
    public Vec3d At(int i, long ms, out double runM)
    {
        runM = SpeedMs[i] * Math.Clamp((ms - LaunchMs[i]) / 1000.0, 0, EndS[i]);
        var (p, f) = (Origin[i], Forward[i]);
        Earth.Advance(ref p, ref f, runM);
        return p;
    }
}

/// <summary>Torpedoes' ends of the last while, newest first: where (on the surface over it), when, the ship hit (an
/// entity, -1 for none), who fired it, its calibre and charge (kg of TNT's worth), its serial (as it ran) and how it ended
/// (docs/visuals/torpedoes.md R0).</summary>
public sealed class TorpedoEndsView
{
    public int Count;
    public Vec3d[] Point = [];
    public long[] Ms = [], Serial = [];
    public int[] Ship = [], Shooter = [];
    public double[] CalibreMm = [], ChargeKg = [];
    public TorpedoEnd[] How = [];

    public void EnsureCapacity(int n)
    {
        if (n <= Point.Length)
            return;
        n = Math.Max(n, Math.Max(16, Point.Length * 2));
        Array.Resize(ref Point, n);
        Array.Resize(ref Ms, n);
        Array.Resize(ref Serial, n);
        Array.Resize(ref Ship, n);
        Array.Resize(ref Shooter, n);
        Array.Resize(ref CalibreMm, n);
        Array.Resize(ref ChargeKg, n);
        Array.Resize(ref How, n);
    }
}

/// <summary>The splashes and hits of the last while, newest first: where, when, the ship hit (an entity, -1 for a
/// splash), who fired and the shell's calibre (the torpedo's, for a torpedo's end); for the world view's splashes
/// (docs/visuals/splashes.md) the salvo, the angle of fall (degrees under the horizontal), the bearing it was going
/// (degrees from north), the shell's kind and its dye.</summary>
public sealed class SplashesView
{
    public int Count;
    public Vec3d[] Point = [];
    public long[] Ms = [];
    public int[] Ship = [], Shooter = [], Salvo = [];
    public double[] CalibreMm = [];
    public float[] FallDeg = [], BearingDeg = [];
    public ShellKind[] Kind = [];
    public Dye[] Dye = [];

    public void EnsureCapacity(int n)
    {
        if (n <= Point.Length)
            return;
        n = Math.Max(n, Math.Max(16, Point.Length * 2));
        Array.Resize(ref Point, n);
        Array.Resize(ref Ms, n);
        Array.Resize(ref Ship, n);
        Array.Resize(ref Shooter, n);
        Array.Resize(ref CalibreMm, n);
        Array.Resize(ref Salvo, n);
        Array.Resize(ref FallDeg, n);
        Array.Resize(ref BearingDeg, n);
        Array.Resize(ref Kind, n);
        Array.Resize(ref Dye, n);
    }
}

/// <summary>The mounts fired of the last while, newest first (docs/visuals/muzzle-flashes.md F0): when, who fired (an
/// entity) and her class (a registry id), which of its mounts, the salvo, how many barrels got a shell away, the
/// mount's axis point, and the way its first shell left (its heading over the sea, unit) and its elevation, degrees.</summary>
public sealed class ShotsView
{
    public int Count;
    public long[] Ms = [];
    public int[] Shooter = [], Class = [], Mount = [], Salvo = [], Barrels = [];
    public Vec3d[] Muzzle = [], Forward = [];
    public float[] ElevationDeg = [];

    public void EnsureCapacity(int n)
    {
        if (n <= Ms.Length)
            return;
        n = Math.Max(n, Math.Max(16, Ms.Length * 2));
        Array.Resize(ref Ms, n);
        Array.Resize(ref Shooter, n);
        Array.Resize(ref Class, n);
        Array.Resize(ref Mount, n);
        Array.Resize(ref Salvo, n);
        Array.Resize(ref Barrels, n);
        Array.Resize(ref Muzzle, n);
        Array.Resize(ref Forward, n);
        Array.Resize(ref ElevationDeg, n);
    }
}

/// <summary>The magazines set off of the last while, newest first (docs/visuals/magazine-explosions.md X0): when, the
/// ship (an entity) and her class (a registry id), the magazine (a room of the class, or -1), what set it off, where she
/// was and her heading over the sea, and the hit's point in her frame.</summary>
public sealed class BlastsView
{
    public int Count;
    public long[] Ms = [];
    public int[] Ship = [], Class = [], Room = [];
    public BlastCause[] Cause = [];
    public Vec3d[] At = [], Forward = [];
    public P3[] Point = [];

    public void EnsureCapacity(int n)
    {
        if (n <= Ms.Length)
            return;
        n = Math.Max(n, Math.Max(4, Ms.Length * 2));
        Array.Resize(ref Ms, n);
        Array.Resize(ref Ship, n);
        Array.Resize(ref Class, n);
        Array.Resize(ref Room, n);
        Array.Resize(ref Cause, n);
        Array.Resize(ref At, n);
        Array.Resize(ref Forward, n);
        Array.Resize(ref Point, n);
    }
}

/// <summary>What shows of a shell's hit from outside (<see cref="HitsView.Shows"/>).</summary>
[Flags]
public enum HitShows : byte
{
    None = 0,
    /// <summary>It struck under the water (its run there ended at the hull).</summary>
    Underwater = 1,
    /// <summary>It left the ship whole, above the water, and flies on (passed through, a ricochet; the exit's point and
    /// way are set).</summary>
    Exit = 2,
    /// <summary>It started a fire.</summary>
    Fire = 4,
    /// <summary>Its burst's gas vented as fast as it formed (an open space): no gas load.</summary>
    Open = 8,
    /// <summary>A plate that held threw spall off its back.</summary>
    Spall = 16,
}

/// <summary>The shells that struck a ship of the last while, newest first (docs/visuals/hits-and-fires.md I0): when, the
/// ship (an entity) and her class (a registry id), who fired, where she was and her heading over the sea at the hit (unit);
/// in her frame (the hitboxes': +x bow, +y starboard, the class's heights) where it struck and which way it was going,
/// where it burst (when it did) and how long after it struck, s, and where it left her and which way (when it flew on,
/// <see cref="HitShows.Exit"/>) at what speed, m/s; the outcome and the burst's kind, the calibre, the shell's kind, its
/// filler's TNT-equivalent W, kg (low-order's force in) and its explosive's RE (black powder's 0.55 to the high explosives'
/// 1 and over: its smoke), the area its burst tore to the sea or air, m², and how many of its burst's pieces weigh 50 g
/// or more (E1's Mott count: those that mark the water where they land). Display only:
/// facts the damage system had at the hit.</summary>
public sealed class HitsView
{
    public int Count;
    public long[] Ms = [];
    public int[] Ship = [], Class = [], Shooter = [];
    public Vec3d[] At = [], Forward = [];
    public P3[] Point = [], Direction = [], BurstAt = [], ExitAt = [], ExitDirection = [];
    public float[] BurstS = [], ExitMs = [], CalibreMm = [], FillerKg = [], FillerRe = [], VentM2 = [], Pieces = [];
    public Combat.Outcome[] Outcome = [];
    public Combat.BurstKind[] Burst = [];
    public ShellKind[] Kind = [];
    public HitShows[] Shows = [];

    public void EnsureCapacity(int n)
    {
        if (n <= Ms.Length)
            return;
        n = Math.Max(n, Math.Max(16, Ms.Length * 2));
        Array.Resize(ref Ms, n);
        Array.Resize(ref Ship, n);
        Array.Resize(ref Class, n);
        Array.Resize(ref Shooter, n);
        Array.Resize(ref At, n);
        Array.Resize(ref Forward, n);
        Array.Resize(ref Point, n);
        Array.Resize(ref Direction, n);
        Array.Resize(ref BurstAt, n);
        Array.Resize(ref ExitAt, n);
        Array.Resize(ref ExitDirection, n);
        Array.Resize(ref BurstS, n);
        Array.Resize(ref ExitMs, n);
        Array.Resize(ref CalibreMm, n);
        Array.Resize(ref FillerKg, n);
        Array.Resize(ref FillerRe, n);
        Array.Resize(ref Pieces, n);
        Array.Resize(ref VentM2, n);
        Array.Resize(ref Outcome, n);
        Array.Resize(ref Burst, n);
        Array.Resize(ref Kind, n);
        Array.Resize(ref Shows, n);
    }
}

/// <summary>The player's navy's picture, as the map draws it (docs/battle/plan.md step 3.2e): per contact the ship it's
/// on (the key: contacts are on true ships in the battle module), the estimate carried on to the snapshot's time (position
/// and velocity) and its error ellipse (1-sigma semi-axes, the major's bearing), whether it's in sight and when it was
/// last seen, how far it's made out, its type, the class it's taken for (a registry id, -1 below class), and its side once
/// its type is made out (255 before); whether her smoke was in sight while she wasn't, and where the last fix was taken
/// from (docs/battle/smoke.md Z6).</summary>
public sealed class ContactsView
{
    public int Count;
    public int[] Entity = [], TakenFor = [];
    public Vec3d[] Position = [], Velocity = [];
    public double[] MajorM = [], MinorM = [], AxisDeg = [];
    public bool[] InSight = [], SmokeInSight = [], LightInSight = [];
    public Vec3d[] From = [];
    public long[] SeenMs = [];
    public ContactLevel[] Level = [];
    public ShipType[] Type = [];
    public byte[] Side = [];
    public SeenFlags[] Seen = [];

    public void EnsureCapacity(int n)
    {
        if (n <= Entity.Length)
            return;
        n = Math.Max(n, Math.Max(16, Entity.Length * 2));
        Array.Resize(ref Entity, n);
        Array.Resize(ref Seen, n);
        Array.Resize(ref TakenFor, n);
        Array.Resize(ref Position, n);
        Array.Resize(ref Velocity, n);
        Array.Resize(ref MajorM, n);
        Array.Resize(ref MinorM, n);
        Array.Resize(ref AxisDeg, n);
        Array.Resize(ref InSight, n);
        Array.Resize(ref SmokeInSight, n);
        Array.Resize(ref LightInSight, n);
        Array.Resize(ref From, n);
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
/// member is detached) and the ship that leads it, its side, and its members (entities or group ids, in order) as a run
/// of <see cref="Members"/>, each with its <see cref="Detached"/>.</summary>
public sealed class GroupsView
{
    public int Count;
    public int[] Id = [], MemberStart = [], MemberCount = [], Members = [], Guide = [], GuideShip = [], Parent = [];

    /// <summary>Its ships' side (-1 for a group with none): a force whose every member is detached has no guide ship, but
    /// a side still.</summary>
    public int[] Side = [];
    public bool[] Force = [];

    /// <summary>Held by the player while its superior's orders for it have changed: a notice, "resume?" (the AI's).</summary>
    public bool[] Asked = [];

    /// <summary>The player has its conn: nothing but the player's helm orders moves its line (the AI's).</summary>
    public bool[] Conn = [];
    public Detachment[] Detached = [];
    public string[] Name = [];
    public Formation[] Formation = [];
    public double[] SpacingM = [];

    /// <summary>A line of bearing's bearing (<see cref="GroupSystem.Group.BearingDeg"/>).</summary>
    public double[] BearingDeg = [];

    /// <summary>Its task (the AI's <c>TaskKind</c>, 0 none) and how it stands (<c>TaskState</c>).</summary>
    public byte[] Task = [], TaskStatus = [];

    /// <summary>A top group under its side's commander (1), held from him by the player (2), or neither (0).</summary>
    public byte[] Commanded = [];

    /// <summary>What it's for (the AI's <c>UnitRole</c>), and whether the player set it (else it's worked out from its ships;
    /// docs/battle/admirals-orders.md O6).</summary>
    public byte[] Role = [];
    public bool[] RoleSet = [];

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
            Array.Resize(ref BearingDeg, n);
            Array.Resize(ref Task, n);
            Array.Resize(ref TaskStatus, n);
            Array.Resize(ref Commanded, n);
            Array.Resize(ref Role, n);
            Array.Resize(ref RoleSet, n);
            Array.Resize(ref Guide, n);
            Array.Resize(ref GuideShip, n);
            Array.Resize(ref Side, n);
            Array.Resize(ref Parent, n);
            Array.Resize(ref Force, n);
            Array.Resize(ref Asked, n);
            Array.Resize(ref Conn, n);
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

    /// <summary>The ship a group's head stands for: its guide ship, else (every member detached) its first ship down
    /// the tree; 0 for none.</summary>
    public int LeadShip(int g)
    {
        if (GuideShip[g] != 0)
            return GuideShip[g];
        for (int i = 0; i < MemberCount[g]; i++)
        {
            int m = Members[MemberStart[g] + i];
            if (!Force[g])
                return m;
            if (IndexOfGroup(m) is int sub and >= 0 && LeadShip(sub) is int s and > 0)
                return s;
        }
        return 0;
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

    /// <summary>Her funnels' smoke against her plant's own: over 1 while she makes smoke (docs/battle/smoke.md Z4), so
    /// what's drawn is what blocks sight; 1 otherwise.</summary>
    public float[] SmokeGain = [];

    /// <summary>Whether she's been ordered to make smoke (<c>SmokeSystem.MakeSmoke</c>).</summary>
    public bool[] MakingSmoke = [];
    public Combat.Helm[] Helm = [];

    /// <summary>Its clock tier: on rails or coarse its pose is now's, a function of time; fine, its tick's.</summary>
    public Combat.ClockTier[] Tier = [];

    /// <summary>When a coarse ship's turn (its segment) ends, ms: carried on past it, it goes straight (long.MaxValue
    /// for none: fine, on rails, riding).</summary>
    public long[] TurnEndMs = [];
    public double[] CourseOrderDeg = [], SpeedOrderKn = [];

    /// <summary>The speed her captain was asked for (NaN: none, or a behaviour sets its own) and the most he may ring,
    /// from above (0: none): what's rung, <see cref="SpeedOrderKn"/>, is never more (the AI's).</summary>
    public double[] SpeedAskedKn = [], SpeedLimitKn = [];

    /// <summary>Who commands each ship and how, filled by the AI (0: nobody, the player's own orders stand); the
    /// meaning of the order is the AI's (<c>Fleetwright.Sim.Ai.CaptainOrder</c>), with its target ship's entity or 0.</summary>
    public byte[] Captain = [];
    public int[] CaptainTarget = [];

    /// <summary>The station her captain keeps on <see cref="CaptainTarget"/> (docs/battle/admirals-orders.md O9b): metres
    /// astern of it and to its starboard, in its frame (the AI's <c>Behaviours.Station</c>); NaN for none (no station, or
    /// a follow at her own lengths' distance). What the map draws a unit's task with: where its ships steer for.</summary>
    public double[] StationAsternM = [], StationAbeamM = [];

    /// <summary>Each ship's fire order (the AI's <c>Fleetwright.Sim.Ai.FireOrder</c>: at will, hold, engage) and the
    /// ship it engages (0: none).</summary>
    public byte[] Fire = [];
    public int[] FireTarget = [];

    /// <summary>Held by the player while its superior's orders for it have changed: a notice, "resume?" (the AI's).</summary>
    public bool[] Asked = [];

    /// <summary>The player has the conn: 1 of her, 2 of her line (her squadron's or a force's above it), 0 not; and the
    /// conn's lookouts: a torpedo's track seen coming at her, its side (1 starboard, -1 port, 0 none) (the AI's).</summary>
    public byte[] Conn = [];
    public sbyte[] Tracks = [];

    /// <summary>Why its captain does what he does, when it isn't simply his order (<c>Fleetwright.Sim.Ai.Captains.CaptainReason</c>:
    /// fallen out, home), 0 for none (the AI's).</summary>
    public byte[] Reason = [];

    /// <summary>A ship alone's task (the AI's <c>TaskKind</c>, 0 none) and how it stands (<c>TaskState</c>).</summary>
    public byte[] Task = [], TaskStatus = [];

    /// <summary>A ship alone under its side's commander (1), held from him by the player (2), or neither (0).</summary>
    public byte[] Commanded = [];

    /// <summary>A ship alone's role (the AI's <c>UnitRole</c>; 0 in a squadron), and whether the player set it.</summary>
    public byte[] Role = [];
    public bool[] RoleSet = [];

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

    /// <summary>Each ship's torpedoes (docs/battle/fighting.md 4.6): in its tubes now, and all it has left.</summary>
    public int[] TorpedoesLoaded = [], TorpedoesLeft = [];

    /// <summary>Each own ship's rounds left by load (docs/battle/shells-armour-damage.md S2): from <see cref="LoadStart"/>
    /// in <see cref="LoadRounds"/>, one per entry of her loadout's pool (<see cref="Loadout"/>: a battery's loads at its
    /// Shells slice), summed over the battery's mounts; int.MaxValue for a load nobody counts; -1 start for a ship not
    /// shown with orders.</summary>
    public int[] LoadStart = [], LoadRounds = [];
    /// <summary>Each own ship's loadout (S5: her class's own, or the one she was given; immutable, so shared); null for a
    /// ship not shown with orders.</summary>
    public Ships.Loadout?[] Loadout = [];
    public int LoadTotal;

    /// <summary>Each own ship's batteries' shells (docs/battle/shell-choice.md A5): from <see cref="LoadStart"/>'s ship's
    /// <see cref="BatteryStart"/>, a battery per the class's batteries in order: the kind ordered and the kind its guns
    /// hold (its first mount's), each the <c>ShellKind</c> + 1, 0 for the magazine order (none for the guns: none left).</summary>
    public int[] BatteryStart = [];
    public byte[] BatteryOrdered = [], BatteryInGuns = [];
    public int BatteryTotal;

    /// <summary>Room for n batteries in all; grows only past the most yet.</summary>
    public void EnsureBatteries(int n)
    {
        if (n > BatteryOrdered.Length)
        {
            int m = Math.Max(n, Math.Max(32, BatteryOrdered.Length * 2));
            Array.Resize(ref BatteryOrdered, m);
            Array.Resize(ref BatteryInGuns, m);
        }
    }

    /// <summary>The player's shell order to a ship's main guns, held against her captain's choice
    /// (docs/battle/shell-choice.md A5, decision 3): the <c>ShellKind</c> + 1, 0 none (by target); and the kind whose
    /// order ended with the rounds of it spent (+ 1, 0 none), until the player orders again (the AI's).</summary>
    public byte[] ShellHeld = [], ShellSpent = [];

    /// <summary>Room for n loads' rounds in all; grows only past the most yet.</summary>
    public void EnsureLoads(int n)
    {
        if (n > LoadRounds.Length)
            Array.Resize(ref LoadRounds, Math.Max(n, Math.Max(64, LoadRounds.Length * 2)));
    }

    /// <summary>Damage as it shows (docs/battle/fighting.md 4.4): each ship's attitude in the water; how it's going down
    /// (None while afloat), from when and till when it's gone under, ms; its parts (the class's components) damaged,
    /// out of action, wrecked, jammed or burning, <see cref="PartCount"/> from <see cref="PartStart"/> in
    /// <see cref="Parts"/>, <see cref="PartStates"/> and <see cref="PartFlags"/>; and its spaces on fire (the class's
    /// cells), <see cref="FireCount"/> from <see cref="FireStart"/> in <see cref="FireCells"/>. An enemy in sight shows
    /// the same, but for her fires: only those that show from outside are listed and flagged burning (docs/visuals/
    /// hits-and-fires.md decision 3: nobody sees inside her hull; her own side knows of the rest).</summary>
    public Combat.Attitude[] Attitude = [];
    public Combat.LossKind[] Loss = [];
    public long[] LossMs = [], GoneMs = [];
    public int[] PartStart = [], PartCount = [], FireStart = [], FireCount = [];
    public int[] Parts = [], FireCells = [];
    public Combat.PartState[] PartStates = [];
    public Combat.PartFlags[] PartFlags = [];
    public int PartTotal, FireTotal;

    /// <summary>The fires that show from outside (docs/battle/fire.md B2, <see cref="Combat.Fires.Shows"/>; docs/visuals/
    /// hits-and-fires.md I0), every ship's alike, <see cref="ShowCount"/> from <see cref="ShowStart"/>: the fire space (the
    /// class's FireSpaces), its heat release and its charges' (MW), its phase, the water on it (L/min) and the share of it
    /// it can boil, where its flames come out in her frame (a hole in her side, the deck over it, the part's top; the
    /// class's heights) and how wide that is, m.</summary>
    public int[] ShowStart = [], ShowCount = [];
    public int[] ShowSpace = [];
    public float[] ShowMW = [], ShowChargesMW = [], ShowWaterLpm = [], ShowBoil = [], ShowWidthM = [];
    public Combat.BurnPhase[] ShowPhase = [];
    public P3[] ShowAt = [];
    public int ShowTotal;

    /// <summary>Room for n showing fires in all; grows only past the most yet.</summary>
    public void EnsureShows(int n)
    {
        if (n <= ShowSpace.Length)
            return;
        n = Math.Max(n, Math.Max(16, ShowSpace.Length * 2));
        Array.Resize(ref ShowSpace, n);
        Array.Resize(ref ShowMW, n);
        Array.Resize(ref ShowChargesMW, n);
        Array.Resize(ref ShowWaterLpm, n);
        Array.Resize(ref ShowBoil, n);
        Array.Resize(ref ShowWidthM, n);
        Array.Resize(ref ShowPhase, n);
        Array.Resize(ref ShowAt, n);
    }

    /// <summary>Room for n damaged parts and n fires in all; grows only past the most yet.</summary>
    public void EnsureDamage(int parts, int fires)
    {
        if (parts > Parts.Length)
        {
            int n = Math.Max(parts, Math.Max(64, Parts.Length * 2));
            Array.Resize(ref Parts, n);
            Array.Resize(ref PartStates, n);
            Array.Resize(ref PartFlags, n);
        }
        if (fires > FireCells.Length)
            Array.Resize(ref FireCells, Math.Max(fires, Math.Max(16, FireCells.Length * 2)));
    }

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
        Array.Resize(ref SmokeGain, n);
        Array.Resize(ref MakingSmoke, n);
        Array.Resize(ref Helm, n);
        Array.Resize(ref Tier, n);
        Array.Resize(ref TurnEndMs, n);
        Array.Resize(ref CourseOrderDeg, n);
        Array.Resize(ref SpeedOrderKn, n);
        Array.Resize(ref SpeedAskedKn, n);
        Array.Resize(ref SpeedLimitKn, n);
        Array.Resize(ref Captain, n);
        Array.Resize(ref CaptainTarget, n);
        Array.Resize(ref StationAsternM, n);
        Array.Resize(ref StationAbeamM, n);
        Array.Resize(ref Fire, n);
        Array.Resize(ref FireTarget, n);
        Array.Resize(ref Asked, n);
        Array.Resize(ref Conn, n);
        Array.Resize(ref Tracks, n);
        Array.Resize(ref Reason, n);
        Array.Resize(ref Task, n);
        Array.Resize(ref TaskStatus, n);
        Array.Resize(ref Commanded, n);
        Array.Resize(ref Role, n);
        Array.Resize(ref RoleSet, n);
        Array.Resize(ref LandSide, n);
        Array.Resize(ref Backstops, n);
        Array.Resize(ref MinClearanceM, n);
        Array.Resize(ref StandoffM, n);
        Array.Resize(ref LookaheadM, n);
        Array.Resize(ref RouteStart, n);
        Array.Resize(ref RouteCount, n);
        Array.Resize(ref RouteLoop, n);
        Array.Resize(ref Attitude, n);
        Array.Resize(ref Loss, n);
        Array.Resize(ref LossMs, n);
        Array.Resize(ref GoneMs, n);
        Array.Resize(ref PartStart, n);
        Array.Resize(ref PartCount, n);
        Array.Resize(ref FireStart, n);
        Array.Resize(ref FireCount, n);
        Array.Resize(ref ShowStart, n);
        Array.Resize(ref ShowCount, n);
        Array.Resize(ref TorpedoesLoaded, n);
        Array.Resize(ref TorpedoesLeft, n);
        Array.Resize(ref LoadStart, n);
        Array.Resize(ref Loadout, n);
        Array.Resize(ref BatteryStart, n);
        Array.Resize(ref ShellHeld, n);
        Array.Resize(ref ShellSpent, n);
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
