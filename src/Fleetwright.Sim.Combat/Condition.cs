using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>What struck a ship.</summary>
public enum HitSource : byte
{
    Shell,
    Torpedo,
    Mine,
    Collision,
    /// <summary>A hit sampled by coarse fire (docs/battle/fighting.md 4.9).</summary>
    Coarse,
}

/// <summary>A hit (docs/battle/fighting.md 4.3a; the architecture's "damage takes a hit, not a shell flight"): the ship
/// struck (an entity), when, where on it and which way the shell was going, both in the ship's own frame (the hitboxes':
/// +x bow, +y starboard, heights over the main deck), its striking speed, what struck it, the shell, and who fired (an
/// entity, and the battery). Torpedoes, mines, collisions and coarse fire's samples come in as more hits.</summary>
public readonly record struct Hit
{
    public int Ship { get; init; }
    public long Ms { get; init; }
    public P3 Point { get; init; }
    /// <summary>A unit vector along the shell's flight.</summary>
    public P3 Direction { get; init; }
    public double SpeedMs { get; init; }
    public HitSource Source { get; init; }
    public ShellTypeRow Shell { get; init; }
    /// <summary>The shell's type in the shooter's class (for the log), or -1.</summary>
    public int ShellType { get; init; }
    public int Shooter { get; init; }
    public int Battery { get; init; }
}

/// <summary>A part's state (a component or a room): working, damaged (working, a room at half), out of action, or
/// wrecked. Nothing repairs yet (repair is fighting.md 4.5's), so a state only gets worse.</summary>
public enum PartState : byte
{
    Ok,
    Damaged,
    Disabled,
    Destroyed,
}

/// <summary>What else is wrong with a part.</summary>
[Flags]
public enum PartFlags : byte
{
    None = 0,
    /// <summary>A mount that can't train (a heavy hit on its turret or barbette that didn't get in).</summary>
    Jammed = 1,
    /// <summary>On fire (it burns from fighting.md 4.3b).</summary>
    Burning = 2,
}

/// <summary>A ship's steering: answering the helm, its rudder jammed at an angle (it circles), or the helm lost (the
/// rudder trails amidships).</summary>
public enum Steering : byte
{
    Free,
    Jammed,
    Lost,
}

/// <summary>Whether a ship is afloat or going down (fighting.md 4.3b settles foundering and capsizing; 4.3a only blows
/// a magazine).</summary>
public enum ShipStatus : byte
{
    Afloat,
    GoingDown,
}

/// <summary>A ship's condition (docs/battle/fighting.md 4.3a): there are no hit points anywhere. Each part damage reaches
/// (the class's nodes: its components, then its rooms) has a state and flags; each watertight cell its water, the area
/// of its holes to the sea, whether it burns and whether its bulkheads are torn; the ship its crew lost, its status and
/// attitude. What it can still do (<see cref="Functions"/>) is worked out from these when they change: which mounts,
/// directors and eyes work, the power it can make and its steering. Allocated at spawn, sized by the class.</summary>
public sealed class ShipCondition
{
    public ShipClass Class { get; private set; } = null!;
    public PartState[] State = [];
    public PartFlags[] Flags = [];
    public double[] WaterM3 = [], BreachM2 = [];
    public bool[] CellFire = [], CellOpened = [];
    public bool[] MountWorks = [], DirectorWorks = [], EyeWorks = [];

    public double CrewLost;
    public ShipStatus Status;
    public Attitude Attitude;
    /// <summary>The share of its power the plant can still make (boilers and shafts), 0-1.</summary>
    public double PowerCap = 1;
    public Steering Steering;
    /// <summary>The rudder's angle when it jammed, radians.</summary>
    public double JamRudderRad;
    /// <summary>Whether it's conned (a bridge or the conning tower works): recorded until the signals (5.1).</summary>
    public bool Conned = true;
    /// <summary>Hits taken, and whether it's been touched at all (a ship never hit hashes as before).</summary>
    public int Hits;
    public bool Damaged;
    /// <summary>Bumped each time what it can do may have changed (the gunnery system rebuilds a battery's instruments).</summary>
    public int Version;

    internal void Reset(ShipClass c)
    {
        if (Class != c)
        {
            Class = c;
            State = new PartState[c.NodeCount];
            Flags = new PartFlags[c.NodeCount];
            WaterM3 = new double[c.Cells.Length];
            BreachM2 = new double[c.Cells.Length];
            CellFire = new bool[c.Cells.Length];
            CellOpened = new bool[c.Cells.Length];
            MountWorks = new bool[c.Mounts.Length];
            DirectorWorks = new bool[c.Directors.Length];
            EyeWorks = new bool[c.Eyes.Length];
        }
        else
        {
            Array.Clear(State);
            Array.Clear(Flags);
            Array.Clear(WaterM3);
            Array.Clear(BreachM2);
            Array.Clear(CellFire);
            Array.Clear(CellOpened);
        }
        Array.Fill(MountWorks, true);
        Array.Fill(DirectorWorks, true);
        Array.Fill(EyeWorks, true);
        (CrewLost, Status, Attitude, PowerCap, Steering, JamRudderRad, Conned, Hits, Damaged) = (0, ShipStatus.Afloat, Attitude.Upright, 1, Steering.Free, 0, true, 0, false);
        Version++;
    }

    /// <summary>A part made worse (never better): the worse of its state and the new one.</summary>
    public bool Worsen(int node, PartState s)
    {
        if (s <= State[node])
            return false;
        State[node] = s;
        return true;
    }

    /// <summary>A part damaged once more: ok to damaged, damaged to disabled.</summary>
    public bool Harm(int node) => Worsen(node, State[node] == PartState.Ok ? PartState.Damaged : PartState.Disabled);

    /// <summary>How much a node still gives: 1 working, 0.5 damaged, 0 out.</summary>
    public double Health(int node) => State[node] switch
    {
        PartState.Ok => 1,
        PartState.Damaged => 0.5,
        _ => 0,
    };

    public bool Has(int node, PartFlags f) => (Flags[node] & f) != 0;

    /// <summary>The working mounts of n (for the strip and tests).</summary>
    public int MountsWorking
    {
        get
        {
            int n = 0;
            foreach (bool w in MountWorks)
                if (w)
                    n++;
            return n;
        }
    }

    public int Fires
    {
        get
        {
            int n = 0;
            foreach (bool f in CellFire)
                if (f)
                    n++;
            foreach (var f in Flags)
                if ((f & PartFlags.Burning) != 0)
                    n++;
            return n;
        }
    }

    public double BreachTotalM2
    {
        get
        {
            double a = 0;
            foreach (double b in BreachM2)
                a += b;
            return a;
        }
    }

    public void Hash(ref StateHash h)
    {
        h.Add(Hits);
        h.Add(CrewLost);
        h.Add((int)Status);
        h.Add(PowerCap);
        h.Add((int)Steering);
        h.Add(JamRudderRad);
        for (int i = 0; i < State.Length; i++)
            if (State[i] != PartState.Ok || Flags[i] != PartFlags.None)
            {
                h.Add(i);
                h.Add((int)State[i] | (int)Flags[i] << 8);
            }
        for (int i = 0; i < BreachM2.Length; i++)
            if (BreachM2[i] != 0 || CellFire[i] || CellOpened[i])
            {
                h.Add(i);
                h.Add(BreachM2[i]);
                h.Add(CellFire[i]);
                h.Add(CellOpened[i]);
            }
    }
}
