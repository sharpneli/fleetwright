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
/// +x bow, +y starboard, heights over the main deck), its striking speed, what struck it, the shell (or the torpedo),
/// and who fired (an entity, and the battery; a torpedo's launcher). Torpedoes (4.6), mines, collisions and coarse fire's
/// samples come in as more hits.</summary>
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
    /// <summary>A torpedo's hit (<see cref="HitSource.Torpedo"/>): the torpedo, its charge and calibre.</summary>
    public TorpedoRow Torpedo { get; init; }
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
    /// <summary>On fire (<see cref="Fires"/>).</summary>
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

/// <summary>Whether a ship is afloat or going down (its magazine gone, foundered or capsized: <see cref="LossKind"/>).
/// A ship settling and listing is still afloat until its buoyancy or stability gives out.</summary>
public enum ShipStatus : byte
{
    Afloat,
    GoingDown,
}

/// <summary>A ship's condition (docs/battle/fighting.md 4.3a, 4.3b): there are no hit points anywhere. Each part damage
/// reaches (the class's nodes: its components, then its rooms) has a state and flags; each watertight cell its water,
/// the area of its holes to the sea (and their mean height and side, ship frame), whether it burns and whether its
/// bulkheads are torn; the ship its crew lost, its status, the water aboard and the attitude and GM it settles to. What
/// it can still do (<see cref="Functions"/>) is worked out from these when they change: which mounts, directors and
/// eyes work, the power it can make and its steering. Allocated at spawn, sized by the class.</summary>
public sealed class ShipCondition
{
    public ShipClass Class { get; private set; } = null!;
    public PartState[] State = [];
    public PartFlags[] Flags = [];
    public double[] WaterM3 = [], BreachM2 = [], BreachY = [], BreachZ = [];
    public bool[] CellFire = [], CellOpened = [];
    public bool[] MountWorks = [], DirectorWorks = [], EyeWorks = [], TubeWorks = [];

    public double CrewLost;
    public ShipStatus Status;
    /// <summary>How it went down (None while afloat), when it began to and when it's gone under (ms).</summary>
    public LossKind Loss;
    public long LossMs, GoneMs;
    public Attitude Attitude;

    /// <summary>Its attitude at a moment (ms): the one it settled to, or going down, how far under it is then
    /// (<see cref="Attitude.At"/>).</summary>
    public Attitude AttitudeAt(long ms) => Attitude.At(Class, Attitude, Loss, LossMs, GoneMs, ms);
    /// <summary>The water aboard, tonnes, and the transverse GM with it in, m (the class's while dry).</summary>
    public double WaterT, GmM;
    /// <summary>The share of its power the plant can still make (boilers and shafts), 0-1.</summary>
    public double PowerCap = 1;
    public Steering Steering;
    /// <summary>The rudder's angle when it jammed, radians.</summary>
    public double JamRudderRad;
    /// <summary>Whether it's conned (a bridge or the conning tower works): recorded until the signals (5.1).</summary>
    public bool Conned = true;
    /// <summary>Hits taken, and whether it's been touched at all (a ship never hit hashes as before); who hit it last (an
    /// entity and battery, for the log's line when it goes down).</summary>
    public int Hits, LastShooter, LastBattery;
    public bool Damaged;
    /// <summary>Bumped each time what it can do may have changed (the gunnery system rebuilds a battery's instruments).</summary>
    public int Version;

    /// <summary>The flooding and fire's clock (the damage system's): when it last stepped, when it steps next (-1: it
    /// doesn't, dry and not burning), the step's generation (a step brought sooner cancels the one it replaces), and the
    /// share of its power it was last held to with its water's weight in (the ship store's).</summary>
    public long StepMs, NextStepMs = -1;
    public int StepGen;
    public double HeldCap = 1;

    internal void Reset(ShipClass c)
    {
        if (Class != c)
        {
            Class = c;
            State = new PartState[c.NodeCount];
            Flags = new PartFlags[c.NodeCount];
            WaterM3 = new double[c.Cells.Length];
            BreachM2 = new double[c.Cells.Length];
            BreachY = new double[c.Cells.Length];
            BreachZ = new double[c.Cells.Length];
            CellFire = new bool[c.Cells.Length];
            CellOpened = new bool[c.Cells.Length];
            MountWorks = new bool[c.Mounts.Length];
            DirectorWorks = new bool[c.Directors.Length];
            EyeWorks = new bool[c.Eyes.Length];
            TubeWorks = new bool[c.Tubes.Length];
        }
        else
        {
            Array.Clear(State);
            Array.Clear(Flags);
            Array.Clear(WaterM3);
            Array.Clear(BreachM2);
            Array.Clear(BreachY);
            Array.Clear(BreachZ);
            Array.Clear(CellFire);
            Array.Clear(CellOpened);
        }
        Array.Fill(MountWorks, true);
        Array.Fill(DirectorWorks, true);
        Array.Fill(EyeWorks, true);
        Array.Fill(TubeWorks, true);
        (CrewLost, Status, Loss, Attitude, PowerCap, Steering, JamRudderRad, Conned, Hits, Damaged) = (0, ShipStatus.Afloat, LossKind.None, Attitude.Upright, 1, Steering.Free, 0, true, 0, false);
        (WaterT, GmM, StepMs, NextStepMs, HeldCap, LastShooter, LastBattery, LossMs, GoneMs) = (0, c.Hydrostatics.GmT, 0, -1, 1, 0, 0, 0, 0);
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

    /// <summary>A hole to the sea in a cell, at a point (ship frame): its area adds, its height and side are the holes'
    /// mean by area.</summary>
    public void Hole(int cell, double areaM2, P3 at)
    {
        double a0 = BreachM2[cell], a1 = a0 + areaM2;
        if (a1 <= 0)
            return;
        BreachY[cell] = (BreachY[cell] * a0 + at.Y * areaM2) / a1;
        BreachZ[cell] = (BreachZ[cell] * a0 + at.Z * areaM2) / a1;
        BreachM2[cell] = a1;
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

    /// <summary>Whether a launcher (the class's Launchers) can fire: its mount or its tube works.</summary>
    public bool LauncherWorks(int launcher)
    {
        ref readonly var l = ref Class.Launchers[launcher];
        return l.Kind == LauncherKind.Mount ? MountWorks[l.Index] : TubeWorks[l.Index];
    }

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
        h.Add((int)Status | (int)Loss << 8);
        h.Add(PowerCap);
        h.Add(WaterT);
        h.Add((int)Steering);
        h.Add(JamRudderRad);
        for (int i = 0; i < State.Length; i++)
            if (State[i] != PartState.Ok || Flags[i] != PartFlags.None)
            {
                h.Add(i);
                h.Add((int)State[i] | (int)Flags[i] << 8);
            }
        for (int i = 0; i < BreachM2.Length; i++)
            if (BreachM2[i] != 0 || CellFire[i] || CellOpened[i] || WaterM3[i] != 0)
            {
                h.Add(i);
                h.Add(BreachM2[i]);
                h.Add(WaterM3[i]);
                h.Add(CellFire[i]);
                h.Add(CellOpened[i]);
            }
    }
}
