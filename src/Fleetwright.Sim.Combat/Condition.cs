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
    /// <summary>Each director's and eye's outage of its own (E4's shock: a director's gyro, a rangefinder knocked out of
    /// adjustment), not its part's: until when, world ms (0: none).</summary>
    public long[] DirectorDown = [], EyeDown = [];
    /// <summary>Openings between spaces a burst's gas left (E5: ruptured boundaries, blown doors, sprung seams), m², on
    /// each edge of the cell graph's entry from its lower cell (the Neighbours pool's index): the flooding adds them.</summary>
    public double[] JoinOpenM2 = [];
    /// <summary>The last cascade's (E5, <see cref="Blast.Cascade"/>): its number, the spaces it reached (BlastRegion, by
    /// BlastStamp) and what each felt, MPa; scratch for the boundaries' marks. E6 reads the pressures.</summary>
    public int BlastId;
    public int[] BlastRegion = [], BlastStamp = [];
    /// <summary>What each reached space felt, MPa, and the gas the region held before it joined, m³ (whether the burst's
    /// hot products got there).</summary>
    public double[] BlastFelt = [], BlastBefore = [];
    internal int[] JoinStamp = [], FaceStamp = [];
    internal byte[] JoinFate = [], FaceFate = [];
    internal double[] JoinFelt = [], FaceFelt = [];
    /// <summary>The class's cells' outside faces (E5): area, plate and middle, worked out at the first burst.</summary>
    internal double[] FaceArea = [], FaceMm = [];
    internal P3[] FacePoint = [];
    internal bool FacesReady;
    /// <summary>When the next timed outage ends, world ms (-1: none does; one for the battle never ends), and the
    /// damage system's event for it: its generation and when it's set for.</summary>
    public long NextClearMs = -1, ClearAtMs = -1;
    public int ClearGen;
    /// <summary>Where its belt leaked from hits that held (E4): the plate loosened there, a ring of the latest
    /// <see cref="LeakMarks"/> (the side plate's row and the point, ship frame).</summary>
    public const int LeakMarks = 32;
    public readonly int[] LeakRow = new int[LeakMarks];
    public readonly P3[] LeakAt = new P3[LeakMarks];
    public int Leaks;

    /// <summary>The crew (E6 of docs/battle/shells-armour-damage.md): the men out of action now (the killed, the wounded
    /// out for the action, the walking wounded not back yet; and the men the fire's and the torpedo's rules take, which
    /// aren't split yet), and the killed and wounded so far.</summary>
    public double CrewLost, Killed, Wounded;
    /// <summary>At each station, the men killed or out there now: a part's (by component), then a cell's
    /// (<see cref="Casualties"/>'s groups).</summary>
    public int[] Gone = [];
    /// <summary>The walking wounded on their way back (E6): when, to which station, how many; a pool of
    /// <see cref="ReturnSlots"/>.</summary>
    public const int ReturnSlots = 64;
    public readonly long[] ReturnMs = new long[ReturnSlots];
    public readonly int[] ReturnGroup = new int[ReturnSlots], ReturnMen = new int[ReturnSlots];
    public int Returns;
    /// <summary>Scratch for one hit's risks on its groups of men (<see cref="Casualties"/>): the log of the share left
    /// unkilled and unhurt by each risk so far, the hit's stamp, the groups it touched.</summary>
    internal double[] RiskK = [], RiskC = [];
    internal int[] RiskStamp = [], Touched = [];
    internal int RiskId, TouchedN;
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
            DirectorDown = new long[c.Directors.Length];
            EyeDown = new long[c.Eyes.Length];
            JoinOpenM2 = new double[c.NeighbourCount];
            (BlastRegion, BlastStamp, BlastFelt) = (new int[c.Cells.Length], new int[c.Cells.Length], new double[c.Cells.Length]);
            BlastBefore = new double[c.Cells.Length];
            (JoinStamp, JoinFate, JoinFelt) = (new int[c.NeighbourCount], new byte[c.NeighbourCount], new double[c.NeighbourCount]);
            (FaceStamp, FaceFate, FaceFelt) = (new int[4 * c.Cells.Length], new byte[4 * c.Cells.Length], new double[4 * c.Cells.Length]);
            (FaceArea, FaceMm, FacePoint, FacesReady) = (new double[4 * c.Cells.Length], new double[4 * c.Cells.Length], new P3[4 * c.Cells.Length], false);
            int groups = c.Components.Length + c.Cells.Length;
            (Gone, RiskK, RiskC, RiskStamp, Touched) = (new int[groups], new double[groups], new double[groups], new int[groups], new int[groups]);
        }
        else
        {
            Array.Clear(Gone);
            Array.Clear(JoinOpenM2);
            Array.Clear(BlastStamp);
            Array.Clear(JoinStamp);
            Array.Clear(FaceStamp);
            Array.Clear(Mount);
            Array.Clear(DownUntil);
            Array.Clear(DirectorDown);
            Array.Clear(EyeDown);
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
        (CrewLost, Killed, Wounded, Returns) = (0, 0, 0, 0);
        (Status, Loss, Attitude, PowerCap, Steering, JamRudderRad, Conned, Hits, Damaged) = (ShipStatus.Afloat, LossKind.None, Attitude.Upright, 1, Steering.Free, 0, true, 0, false);
        (WaterT, GmM, StepMs, NextStepMs, HeldCap, LastShooter, LastBattery, LossMs, GoneMs) = (0, c.Hydrostatics.GmT, 0, -1, 1, 0, 0, 0, 0);
        (NextClearMs, ClearAtMs, Leaks) = (-1, -1, 0);
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

    /// <summary>A director or an eye out of its own for a while, from now for ms (E4's shock): until then, or later if it
    /// already was.</summary>
    public void Out(long[] downs, int i, long now, long ms)
    {
        if (ms <= 0)
            return;
        long until = Until(now, ms);
        downs[i] = Math.Max(downs[i], until);
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

    /// <summary>A leak in a belt's plate at a point (E4): the plate loosened there.</summary>
    public void Leaked(int row, P3 at)
    {
        int i = Leaks++ % LeakMarks;
        (LeakRow[i], LeakAt[i]) = (row, at);
    }

    /// <summary>Whether a belt's plate leaked within m of a point before: loosened there (E4).</summary>
    public bool Loosened(int row, P3 at, double m)
    {
        for (int i = 0, n = Math.Min(Leaks, LeakMarks); i < n; i++)
        {
            ref readonly var p = ref LeakAt[i];
            if (LeakRow[i] == row && (p.X - at.X) * (p.X - at.X) + (p.Y - at.Y) * (p.Y - at.Y) + (p.Z - at.Z) * (p.Z - at.Z) <= m * m)
                return true;
        }
        return false;
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
        any |= Clear(DirectorDown, now, ref next) | Clear(EyeDown, now, ref next);
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
        // the walking wounded back at their stations (nothing the ship can do changes with them yet)
        for (int i = Returns - 1; i >= 0; i--)
        {
            if (ReturnMs[i] <= now)
                Return(i);
            else
                next = Math.Min(next, ReturnMs[i]);
        }
        NextClearMs = next is long.MaxValue ? -1 : next;
        return any;
    }

    /// <summary>Walking wounded leaving a station, back at it then (E6): one more on the way back; with the pool full the
    /// soonest due come back now to make room.</summary>
    public void Back(int group, int men, long at)
    {
        if (men <= 0)
            return;
        if (Returns == ReturnSlots)
        {
            int soonest = 0;
            for (int i = 1; i < Returns; i++)
                if (ReturnMs[i] < ReturnMs[soonest])
                    soonest = i;
            Return(soonest);
        }
        (ReturnMs[Returns], ReturnGroup[Returns], ReturnMen[Returns]) = (at, group, men);
        Returns++;
        Due(at);
    }

    /// <summary>A return's men back at their station, its slot taken by the last.</summary>
    void Return(int i)
    {
        int g = ReturnGroup[i], men = Math.Min(ReturnMen[i], Gone[g]);
        Gone[g] -= men;
        CrewLost = Math.Max(CrewLost - men, 0);
        Returns--;
        (ReturnMs[i], ReturnGroup[i], ReturnMen[i]) = (ReturnMs[Returns], ReturnGroup[Returns], ReturnMen[Returns]);
    }

    /// <summary>A row of outages ended by now (zeroed), the next of the rest's end; whether any ended.</summary>
    static bool Clear(long[] downs, long now, ref long next)
    {
        bool any = false;
        for (int i = 0; i < downs.Length; i++)
        {
            if (downs[i] == 0)
                continue;
            if (downs[i] <= now)
                (downs[i], any) = (0, true);
            else
                next = Math.Min(next, Later(downs[i], now));
        }
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
        if (Killed + Wounded > 0)
        {
            h.Add(Killed);
            h.Add(Wounded);
        }
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
        if (Leaks > 0)
            h.Add(Leaks);
        for (int i = 0; i < JoinOpenM2.Length; i++)
            if (JoinOpenM2[i] != 0)
            {
                h.Add(i + 2000);
                h.Add(JoinOpenM2[i]);
            }
        for (int i = 0; i < DirectorDown.Length; i++)
            if (DirectorDown[i] != 0)
            {
                h.Add(i);
                h.Add(DirectorDown[i]);
            }
        for (int i = 0; i < EyeDown.Length; i++)
            if (EyeDown[i] != 0)
            {
                h.Add(i + 1000);
                h.Add(EyeDown[i]);
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
