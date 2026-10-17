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
    /// <summary>How far the shell ran under the water before it met the hull, m (0: it struck in the air), its
    /// <see cref="SpeedMs"/> where it met the water (<see cref="WaterRun"/>). Its own path starts just short of the hull,
    /// so only the shell's flight knows it.</summary>
    public double WaterRunM { get; init; }
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
    /// <summary>Out for a while (its crew stunned, its power tripped): it gives nothing until
    /// <see cref="ShipCondition.DownUntil"/> (docs/battle/shells-armour-damage.md E4).</summary>
    Down = 4,
}

/// <summary>What hits that held left a mount (docs/battle/shells-armour-damage.md E4; damage/13 §1): jammed in train (its
/// component's <see cref="PartFlags.Jammed"/>, by a cause) until a time, stiff until one, guns out until one, its sights
/// gone. Times are world ms; 0 none, <see cref="NonPen.Battle"/> the rest of the battle.</summary>
public struct MountHarm
{
    public JamCause Jam;
    public long JamUntil, StiffUntil, GunUntil;
    public int GunsOut;
    public bool Sights;
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
    /// <summary>What hits that held left each mount (E4).</summary>
    public MountHarm[] Mount = [];
    /// <summary>Each node's outage while it's <see cref="PartFlags.Down"/>: until when, world ms.</summary>
    public long[] DownUntil = [];
    /// <summary>When the next timed outage ends, world ms (-1: none does; one for the battle never ends), and the
    /// damage system's event for it: its generation and when it's set for.</summary>
    public long NextClearMs = -1, ClearAtMs = -1;
    public int ClearGen;

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
            Mount = new MountHarm[c.Mounts.Length];
            DownUntil = new long[c.NodeCount];
        }
        else
        {
            Array.Clear(Mount);
            Array.Clear(DownUntil);
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
        (NextClearMs, ClearAtMs) = (-1, -1);
        ClearGen++;
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

    /// <summary>How much a node still gives: 1 working, 0.5 damaged, 0 out (or down for a while).</summary>
    public double Health(int node) => (Flags[node] & PartFlags.Down) != 0 ? 0 : State[node] switch
    {
        PartState.Ok => 1,
        PartState.Damaged => 0.5,
        _ => 0,
    };

    public bool Has(int node, PartFlags f) => (Flags[node] & f) != 0;

    /// <summary>A node out for a while, from now for ms (E4): down until then, or later if it already was.</summary>
    public void Down(int node, long now, long ms)
    {
        if (ms <= 0)
            return;
        long until = Until(now, ms);
        Flags[node] |= PartFlags.Down;
        DownUntil[node] = Math.Max(DownUntil[node], until);
        Due(until);
    }

    /// <summary>What a hit that held did to a mount's turret, from now (E4): jammed (the later of its jams lasting, with
    /// its cause), stiff, a gun more out (no more than it has), its sights gone; its crew stunned with it.</summary>
    public void Strike(int mount, in TurretHarm h, long now)
    {
        ref var a = ref Mount[mount];
        ref readonly var row = ref Class.Mounts[mount];
        long until = Until(now, h.Ms);
        switch (h.Blow)
        {
            case TurretBlow.Jam:
                Flags[row.Component] |= PartFlags.Jammed;
                if (until > a.JamUntil)
                    (a.JamUntil, a.Jam) = (until, h.Cause);
                Due(until);
                break;
            case TurretBlow.Stiff:
                a.StiffUntil = Math.Max(a.StiffUntil, until);
                Due(until);
                break;
            case TurretBlow.GunOut:
                a.GunsOut = Math.Min(a.GunsOut + 1, Class.TurretTypes[row.Type].Barrels);
                a.GunUntil = Math.Max(a.GunUntil, until);
                Due(until);
                break;
            case TurretBlow.Sights:
                a.Sights = true;
                break;
        }
        Down(row.Component, now, h.StunMs);
    }

    /// <summary>Whether a mount trains stiffly now (E4).</summary>
    public bool Stiff(int mount, long now) => Mount[mount].StiffUntil > now;

    /// <summary>The guns a mount still fires of its barrels (E4: its guns out don't).</summary>
    public int Guns(int mount) => Math.Max(Class.TurretTypes[Class.Mounts[mount].Type].Barrels - Mount[mount].GunsOut, 0);

    /// <summary>Whether a mount can lay its guns: it works, and at local control, with its sights (E4: a mount with its
    /// sights out fires only by a director).</summary>
    public bool Lays(int mount, bool local) => MountWorks[mount] && !(local && Mount[mount].Sights);

    /// <summary>The timed outages that have run out by now ended (E4): nodes no longer down, jams cleared, stiffness and
    /// guns out over; when the next ends. Whether anything ended.</summary>
    public bool Clear(long now)
    {
        bool any = false;
        long next = long.MaxValue;
        for (int i = 0; i < DownUntil.Length; i++)
        {
            if ((Flags[i] & PartFlags.Down) == 0)
                continue;
            if (DownUntil[i] <= now)
                (Flags[i], DownUntil[i], any) = (Flags[i] & ~PartFlags.Down, 0, true);
            else
                next = Math.Min(next, DownUntil[i]);
        }
        var mounts = Class.Mounts;
        for (int m = 0; m < Mount.Length; m++)
        {
            ref var a = ref Mount[m];
            if (a.JamUntil > 0 && a.JamUntil <= now)
            {
                Flags[mounts[m].Component] &= ~PartFlags.Jammed;
                (a.JamUntil, a.Jam, any) = (0, JamCause.None, true);
            }
            if (a.StiffUntil > 0 && a.StiffUntil <= now)
                (a.StiffUntil, any) = (0, true);
            if (a.GunUntil > 0 && a.GunUntil <= now)
                (a.GunUntil, a.GunsOut, any) = (0, 0, true);
            next = Math.Min(next, Later(a.JamUntil, now));
            next = Math.Min(next, Later(a.StiffUntil, now));
            next = Math.Min(next, Later(a.GunUntil, now));
        }
        NextClearMs = next is long.MaxValue ? -1 : next;
        return any;
    }

    /// <summary>An outage's end if it's still to come and ends at all, else never (long.MaxValue).</summary>
    static long Later(long until, long now) => until > now ? until : long.MaxValue;

    /// <summary>An outage's end, from now for ms (<see cref="NonPen.Battle"/> stays).</summary>
    static long Until(long now, long ms) => ms == NonPen.Battle ? NonPen.Battle : now + ms;

    /// <summary>An outage ending then: the next to end, if it's sooner (one for the battle never ends).</summary>
    void Due(long until)
    {
        if (until != NonPen.Battle && until > 0 && (NextClearMs < 0 || until < NextClearMs))
            NextClearMs = until;
    }

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
                if ((Flags[i] & PartFlags.Down) != 0)
                    h.Add(DownUntil[i]);
            }
        for (int m = 0; m < Mount.Length; m++)
        {
            ref readonly var a = ref Mount[m];
            if (a.JamUntil == 0 && a.StiffUntil == 0 && a.GunsOut == 0 && !a.Sights)
                continue;
            h.Add(m);
            h.Add((int)a.Jam | a.GunsOut << 8 | (a.Sights ? 1 << 16 : 0));
            h.Add(a.JamUntil);
            h.Add(a.StiffUntil);
            h.Add(a.GunUntil);
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
