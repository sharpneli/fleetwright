using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>A mount's state (docs/battle/fighting.md 4.2b): its train and elevation as analytic anchors (at the
/// anchor's time it was at From, moving toward To at the mount's rate, so its bearing at any instant is closed form and
/// nothing steps it), when its guns are next loaded, and the rounds it has left. Bearings are degrees clockwise from
/// ahead, unwrapped inside the mount's traverse (an interval that never wraps: shipgen's <c>traverse_deg</c>).</summary>
public struct MountState
{
    public long TrainMs, ElevMs, LoadedMs;
    public double TrainFrom, TrainTo, ElevFrom, ElevTo;
    public int Rounds;
}

/// <summary>Each ship's combat columns beside its <see cref="ShipStore"/> (docs/battle/fighting.md "Where it lives"):
/// its mounts' state, made at spawn and sized by the class, how many of its batteries are engaged and on what. Reached through
/// <see cref="ShipState"/>'s accessors and the gunnery system; and its condition (the damage system's, 4.3a).</summary>
public sealed class CombatStore
{
    MountState[]?[] mounts = [];
    ShipCondition?[] conditions = [];
    int[]?[] targets = [];
    int[] engaged = [];
    bool[] moved = [];

    /// <summary>Whether any mount has ever moved or fired (so a world that never fought hashes as before).</summary>
    public bool Touched { get; private set; }

    internal void Spawn(int slot, ShipClass cls, long ms)
    {
        if (slot >= mounts.Length)
        {
            int n = Math.Max(slot + 1, Math.Max(16, mounts.Length * 2));
            Array.Resize(ref mounts, n);
            Array.Resize(ref conditions, n);
            Array.Resize(ref targets, n);
            Array.Resize(ref engaged, n);
            Array.Resize(ref moved, n);
        }
        var m = mounts[slot];
        if (m == null || m.Length != cls.Mounts.Length)
            mounts[slot] = m = new MountState[cls.Mounts.Length];
        for (int i = 0; i < m.Length; i++)
        {
            ref readonly var row = ref cls.Mounts[i];
            double rest = Into(row, row.RestDeg, row.RestDeg);
            m[i] = new MountState
            {
                TrainMs = ms, TrainFrom = rest, TrainTo = rest, ElevMs = ms, LoadedMs = ms,
                Rounds = row.Rounds > 0 ? row.Rounds : int.MaxValue,
            };
        }
        (conditions[slot] ??= new ShipCondition()).Reset(cls);
        var t = targets[slot];
        if (t == null || t.Length != cls.Batteries.Length)
            targets[slot] = t = new int[cls.Batteries.Length];
        Array.Clear(t);
        engaged[slot] = 0;
        moved[slot] = false;
    }

    internal void Drop(int slot) => engaged[slot] = 0;

    /// <summary>The ship (entity) a ship's battery is engaged on, 0 for none.</summary>
    public int Target(int slot, int battery) => targets[slot] is { } t && battery < t.Length ? t[battery] : 0;

    /// <summary>A ship's condition (docs/battle/fighting.md 4.3a): its parts, cells and what it can still do.</summary>
    public ShipCondition Condition(int slot) => conditions[slot]!;

    /// <summary>A ship's mounts (the class's order).</summary>
    public Span<MountState> Mounts(int slot) => mounts[slot];

    public ref MountState Mount(int slot, int mount) => ref mounts[slot]![mount];

    /// <summary>How many of a ship's batteries are engaged (the rails' rule reads it: an engaged ship stays off rails).</summary>
    public int Engaged(int slot) => slot < engaged.Length ? engaged[slot] : 0;

    /// <summary>A ship's battery engaged on a ship (an entity), or freed (0).</summary>
    public void Engage(int slot, int battery, int target)
    {
        ref int t = ref targets[slot]![battery];
        engaged[slot] = Math.Max(engaged[slot] + (target != 0 ? 1 : 0) - (t != 0 ? 1 : 0), 0);
        t = target;
    }

    /// <summary>Whether a ship's mounts have left their rest (the snapshot sends their bearings only then).</summary>
    public bool Moved(int slot) => slot < moved.Length && moved[slot];

    // ------------------------------------------------------------------ the analytic mount

    /// <summary>A mount's bearing at a time, degrees from ahead (unwrapped in its traverse).</summary>
    public static double BearingAt(in MountState s, in MountRow m, long ms) => Toward(s.TrainFrom, s.TrainTo, m.TrainDegS, s.TrainMs, ms);

    public static double ElevationAt(in MountState s, in MountRow m, long ms) => Toward(s.ElevFrom, s.ElevTo, m.ElevateDegS, s.ElevMs, ms);

    static double Toward(double from, double to, double rate, long since, long ms)
    {
        double d = to - from, run = rate * Math.Max(ms - since, 0) / 1000;
        if (d == 0)
            return to;   // nowhere to go (a mount with no rate, a torpedo mount's, too)
        return Math.Abs(d) <= run ? to : from + Math.Sign(d) * run;
    }

    /// <summary>When a mount gets to its ordered train and elevation, ms.</summary>
    public static long LaidBy(in MountState s, in MountRow m) =>
        Math.Max(s.TrainMs + (long)Math.Ceiling(Math.Abs(s.TrainTo - s.TrainFrom) / m.TrainDegS * 1000),
            s.ElevMs + (long)Math.Ceiling(Math.Abs(s.ElevTo - s.ElevFrom) / m.ElevateDegS * 1000));

    /// <summary>Orders a ship's mount to a bearing (degrees from ahead) and an elevation now: re-anchored where it is,
    /// moving on toward the bearing as its traverse lets it (the nearer end if the bearing is outside it) and the
    /// elevation within its own; whether the bearing is inside the traverse.</summary>
    public bool Order(int slot, int mount, in MountRow m, long ms, double bearingDeg, double elevationDeg)
    {
        ref var s = ref mounts[slot]![mount];
        double b = BearingAt(s, m, ms), e = ElevationAt(s, m, ms);
        double to = Into(m, bearingDeg, b);
        (s.TrainMs, s.TrainFrom, s.TrainTo) = (ms, b, to);
        (s.ElevMs, s.ElevFrom, s.ElevTo) = (ms, e, Math.Clamp(elevationDeg, -5, m.MaxElevationDeg));
        moved[slot] = Touched = true;
        return Within(m, to, bearingDeg);
    }

    /// <summary>A mount stopped where it is now (out of action, jammed: it trains no more), its train and elevation held
    /// there.</summary>
    public void Stop(int slot, int mount, in MountRow m, long ms)
    {
        ref var s = ref mounts[slot]![mount];
        double b = BearingAt(s, m, ms), e = ElevationAt(s, m, ms);
        if (b == s.TrainTo && e == s.ElevTo || !double.IsFinite(b) || !double.IsFinite(e))
            return;
        (s.TrainMs, s.TrainFrom, s.TrainTo) = (ms, b, b);
        (s.ElevMs, s.ElevFrom, s.ElevTo) = (ms, e, e);
    }

    /// <summary>A gun fired: its mount loads again for the reload, and its rounds go down.</summary>
    public void Fired(int slot, int mount, in MountRow m, int guns, long ms)
    {
        ref var s = ref mounts[slot]![mount];
        s.LoadedMs = ms + (long)Math.Round(m.ReloadS * 1000);
        s.Rounds = s.Rounds == int.MaxValue ? s.Rounds : Math.Max(s.Rounds - guns, 0);
        Touched = true;
    }

    /// <summary>A bearing in a mount's traverse: the turn of it inside the interval (nearest the current bearing for a
    /// mount that turns all the way round), or the interval's nearer end.</summary>
    static double Into(in MountRow m, double bearingDeg, double current)
    {
        if (double.IsNaN(m.TraverseFromDeg))
            return current + Wrap(bearingDeg - current);
        double lo = m.TraverseFromDeg, hi = m.TraverseToDeg;
        double b = lo + Mod(bearingDeg - lo);
        if (b <= hi)
            return b;
        // outside: the nearer end, by the angle round to each
        return Mod(bearingDeg - hi) <= Mod(lo - bearingDeg) ? hi : lo;
    }

    static bool Within(in MountRow m, double into, double bearingDeg) => Math.Abs(Wrap(into - bearingDeg)) < 1e-9;

    /// <summary>Whether a relative bearing is in one of a mount's arcs of fire (pairs of degrees clockwise from
    /// ahead).</summary>
    public static bool InArcs(ShipClass cls, in MountRow m, double bearingDeg)
    {
        var a = cls.Numbers(m.Arcs);
        if (a.Length == 0)
            return true;
        for (int i = 0; i + 1 < a.Length; i += 2)
        {
            double span = a[i + 1] - a[i];
            if (span >= 360 || Mod(bearingDeg - a[i]) <= span)
                return true;
        }
        return false;
    }

    static double Mod(double d) => (d % 360 + 360) % 360;

    static double Wrap(double d) => d - 360 * Math.Round(d / 360);

    public void Hash(ref StateHash h, ShipStore store)
    {
        if (!Touched)
            return;
        for (int slot = 0; slot < store.Slots; slot++)
        {
            if (!store.IsAlive(slot) || !Moved(slot) && engaged[slot] == 0)
                continue;
            h.Add(slot);
            h.Add(engaged[slot]);
            foreach (ref readonly var s in mounts[slot].AsSpan())
            {
                h.Add(s.TrainMs);
                h.Add(s.TrainFrom);
                h.Add(s.TrainTo);
                h.Add(s.ElevTo);
                h.Add(s.LoadedMs);
                h.Add(s.Rounds);
            }
        }
    }
}
