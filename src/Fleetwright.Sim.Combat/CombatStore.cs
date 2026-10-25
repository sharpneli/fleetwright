using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>A mount's state (docs/battle/fighting.md 4.2b): its train and elevation as analytic anchors (at the
/// anchor's time it was at From, moving toward To at the mount's rate, so its bearing at any instant is closed form and
/// nothing steps it), when its guns are next loaded, and the rounds it has left (all kinds; by kind in
/// <see cref="CombatStore.LoadRounds"/>). Bearings are degrees clockwise from
/// ahead, unwrapped inside the mount's traverse (an interval that never wraps: shipgen's <c>traverse_deg</c>). Stiff
/// (docs/battle/shells-armour-damage.md E4): the share of its training rate a hit that held took (0 none). Its ready-use
/// rounds (fire-control.md F6d), as they stood at <see cref="ReadyMs"/>: the hoists fill them back at their rate. What
/// its guns hold (docs/battle/shell-choice.md A1): <see cref="InGuns"/>.</summary>
public struct MountState
{
    public long TrainMs, ElevMs, LoadedMs, ReadyMs;
    public double TrainFrom, TrainTo, ElevFrom, ElevTo;
    public int Rounds;
    /// <summary>The load its guns were loaded with, its index in the battery's loads + 1; 0 when none was chosen (no
    /// load ordered when they loaded), so it fires the magazine order's next (<see cref="CombatStore.FiresNext"/>).</summary>
    public int InGuns;
    public double Stiff, Ready;
}

/// <summary>A torpedo launcher's state (docs/battle/fighting.md 4.6): the torpedoes in its tubes, the spares it carries
/// beside them, when its tubes are loaded again from them (they load all at once), and when it last fired (0: never).</summary>
public struct LauncherState
{
    public int Loaded, Spare;
    public long LoadedMs, FiredMs;
}

/// <summary>Each ship's combat columns beside its <see cref="ShipStore"/> (docs/battle/fighting.md "Where it lives"):
/// its mounts' state, made at spawn and sized by the class, how many of its batteries are engaged and on what. Reached through
/// <see cref="ShipState"/>'s accessors and the gunnery system; and its condition (the damage system's, 4.3a).</summary>
public sealed class CombatStore
{
    MountState[]?[] mounts = [];
    Loadout?[] loadouts = [];
    int[]?[] loadRounds = [];
    byte[]?[] ordered = [];   // by battery, the load ordered: its kind + 1, 0 the magazine order
    LauncherState[]?[] launchers = [];
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
            Array.Resize(ref loadouts, n);
            Array.Resize(ref loadRounds, n);
            Array.Resize(ref ordered, n);
            Array.Resize(ref launchers, n);
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
                TrainMs = ms, TrainFrom = rest, TrainTo = rest, ElevMs = ms, LoadedMs = ms, ReadyMs = ms, Ready = row.ReadyRounds,
                Rounds = row.Rounds > 0 ? row.Rounds : int.MaxValue,
            };
        }
        Fill(slot, cls.Loadout);
        var l = launchers[slot];
        if (l == null || l.Length != cls.Launchers.Length)
            launchers[slot] = l = new LauncherState[cls.Launchers.Length];
        for (int i = 0; i < l.Length; i++)
        {
            ref readonly var row = ref cls.Launchers[i];
            int loaded = Math.Min(row.Tubes, row.Torpedoes);
            l[i] = new LauncherState { Loaded = loaded, Spare = row.Torpedoes - loaded, LoadedMs = ms };
        }
        (conditions[slot] ??= new ShipCondition()).Reset(cls);
        var t = targets[slot];
        if (t == null || t.Length != cls.Batteries.Length)
            targets[slot] = t = new int[cls.Batteries.Length];
        Array.Clear(t);
        var o = ordered[slot];
        if (o == null || o.Length != cls.Batteries.Length)
            ordered[slot] = new byte[cls.Batteries.Length];
        else
            Array.Clear(o);
        engaged[slot] = 0;
        moved[slot] = false;
    }

    internal void Drop(int slot) => engaged[slot] = 0;

    /// <summary>A ship's loadout (docs/battle/shells-armour-damage.md S5): her class's own unless she was given another.
    /// What her guns fire, her rounds by load and the matchup's judgement of her read it.</summary>
    public Loadout Loadout(int slot) => loadouts[slot]!;

    /// <summary>A ship takes another loadout of her class, her magazines filled with it: only while she has fired nothing
    /// of the one she has (in port; until the campaign's ports, at spawn). False, and nothing changed, otherwise.</summary>
    public bool Load(int slot, Loadout l)
    {
        var now = loadouts[slot]!;
        if (!ReferenceEquals(l.Class, now.Class) || !now.LoadRounds.SequenceEqual(loadRounds[slot]))
            return false;
        Fill(slot, l);
        return true;
    }

    void Fill(int slot, Loadout l)
    {
        loadouts[slot] = l;
        var lr = loadRounds[slot];
        if (lr == null || lr.Length != l.LoadRounds.Length)
            loadRounds[slot] = lr = new int[l.LoadRounds.Length];
        l.LoadRounds.CopyTo(lr);
    }

    /// <summary>The ship (entity) a ship's battery is engaged on, 0 for none.</summary>
    public int Target(int slot, int battery) => targets[slot] is { } t && battery < t.Length ? t[battery] : 0;

    /// <summary>A ship's condition (docs/battle/fighting.md 4.3a): its parts, cells and what it can still do.</summary>
    public ShipCondition Condition(int slot) => conditions[slot]!;

    /// <summary>A ship's mounts (the class's order).</summary>
    public Span<MountState> Mounts(int slot) => mounts[slot];

    public ref MountState Mount(int slot, int mount) => ref mounts[slot]![mount];

    /// <summary>A ship's torpedo launcher (the class's Launchers) as it is at a time: its tubes loaded again from its
    /// spares once their time has come (read only: the store keeps it as it was last fired).</summary>
    public LauncherState Launcher(int slot, int launcher, in LauncherRow row, long ms)
    {
        var l = launchers[slot]![launcher];
        if (l.Spare > 0 && l.Loaded < row.Tubes && ms >= l.LoadedMs)
        {
            int n = Math.Min(row.Tubes - l.Loaded, l.Spare);
            (l.Loaded, l.Spare) = (l.Loaded + n, l.Spare - n);
        }
        return l;
    }

    /// <summary>A launcher fired n torpedoes now: its tubes load again from its spares after its reload.</summary>
    public void Launched(int slot, int launcher, in LauncherRow row, int n, long ms)
    {
        ref var l = ref launchers[slot]![launcher];
        l = Launcher(slot, launcher, row, ms);
        (l.Loaded, l.FiredMs) = (Math.Max(l.Loaded - n, 0), ms);
        if (l.Spare > 0)
            l.LoadedMs = ms + (long)Math.Round(row.ReloadS * 1000);
        Touched = true;
        moved[slot] = true;
    }

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
    public static double BearingAt(in MountState s, in MountRow m, long ms) => Toward(s.TrainFrom, s.TrainTo, TrainRate(s, m), s.TrainMs, ms);

    /// <summary>A mount's training rate now, degrees a second: its own, less what stiffness took (E4).</summary>
    public static double TrainRate(in MountState s, in MountRow m) => m.TrainDegS * (1 - s.Stiff);

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
        Math.Max(s.TrainMs + (long)Math.Ceiling(Math.Abs(s.TrainTo - s.TrainFrom) / TrainRate(s, m) * 1000),
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

    /// <summary>Orders a torpedo mount to a bearing now (it trains, it doesn't elevate): when it gets there, ms (the
    /// nearer end of its traverse if the bearing is outside it), and whether it's inside it.</summary>
    public bool Train(int slot, int mount, in MountRow m, long ms, double bearingDeg, out long laidMs)
    {
        ref var s = ref mounts[slot]![mount];
        double b = BearingAt(s, m, ms);
        double to = Into(m, bearingDeg, b);
        (s.TrainMs, s.TrainFrom, s.TrainTo) = (ms, b, to);
        laidMs = ms + (m.TrainDegS > 0 ? (long)Math.Ceiling(Math.Abs(to - b) / TrainRate(s, m) * 1000) : 0);
        moved[slot] = Touched = true;
        return Within(m, to, bearingDeg);
    }

    /// <summary>A mount's stiffness set now (E4: the share of its training rate lost), re-anchored where it is so its
    /// bearing so far keeps the rate it had.</summary>
    public void SetStiff(int slot, int mount, in MountRow m, long ms, double stiff)
    {
        ref var s = ref mounts[slot]![mount];
        if (s.Stiff == stiff)
            return;
        double b = BearingAt(s, m, ms);
        (s.TrainMs, s.TrainFrom, s.Stiff) = (ms, b, stiff);
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

    /// <summary>A mount fired its guns of a load (its index in the battery's loads, <see cref="NextLoad"/>): it loads
    /// again, and its rounds go down. The next rounds come from its ready-use rounds while it has them, at the reload (the
    /// drill's times the crew's battle factor, <paramref name="loadFactor"/>); after, as the hoists bring them, one a gun
    /// every <see cref="MountRow.SupplyS"/> (fire-control.md F6d), the ready-use rounds filling back at that rate.</summary>
    public void Fired(int slot, ShipClass cls, int mount, int load, int guns, long ms, double loadFactor = 1, double supplyFactor = 1)
    {
        ref var s = ref mounts[slot]![mount];
        ref readonly var row = ref cls.Mounts[mount];
        double reload = row.ReloadS * loadFactor;
        if (row.ReadyRounds > 0 && row.SupplyS > 0)
        {
            // rounds a second; the side's flash discipline hurries or slows the hoists (docs/battle/magazines.md V5)
            double hoist = cls.TurretTypes[row.Type].Barrels / (row.SupplyS * supplyFactor);
            double ready = Math.Min(row.ReadyRounds, s.Ready + hoist * Math.Max(ms - s.ReadyMs, 0) / 1000.0), wait = 0;
            if (ready >= guns)
                ready -= guns;
            else
                (wait, ready) = ((guns - ready) / hoist, 0);
            (s.Ready, s.ReadyMs) = (ready, ms + (long)(wait * 1000));
            reload = Math.Max(reload, wait);
        }
        s.LoadedMs = ms + (long)Math.Round(reload * 1000);
        Spend(slot, mount, load, guns);
        // and loads again: with the load ordered while it has rounds of it, else by the magazine order when it fires
        s.InGuns = ordered[slot]![row.Battery] is > 0 and var k ? LoadOf(slot, mount, (ShellKind)(k - 1)) + 1 : 0;
    }

    /// <summary>The kind a battery's guns load with (docs/battle/shell-choice.md A2), null for the magazine order.</summary>
    public ShellKind? Ordered(int slot, int battery) => ordered[slot] is { } o && battery < o.Length && o[battery] > 0 ? (ShellKind)(o[battery] - 1) : null;

    /// <summary>A battery loads with a kind (null: by the magazine order) from now on. What its guns hold is fired first
    /// (decision 1: a change costs the round in the gun): a mount loaded by the magazine order keeps the round it would
    /// fire next.</summary>
    public void Order(int slot, int battery, ShellKind? kind)
    {
        var o = ordered[slot]!;
        byte k = kind is { } x ? (byte)(x + 1) : (byte)0;
        if (battery < 0 || battery >= o.Length || o[battery] == k)
            return;
        var cls = loadouts[slot]!.Class;
        var m = mounts[slot]!;
        foreach (int i in cls.Links(cls.Batteries[battery].Mounts))
            if (m[i].InGuns == 0)
                m[i].InGuns = NextLoad(slot, i) + 1;
        o[battery] = k;
        Touched = true;
    }

    /// <summary>The load a mount fires now: what its guns hold while it has rounds of it (a round spent outside its
    /// loading, the dev's salvo, may leave none), else the magazine order's next (<see cref="NextLoad"/>); -1 with none
    /// left.</summary>
    public int FiresNext(int slot, int mount)
    {
        int k = mounts[slot]![mount].InGuns - 1;
        return k >= 0 && LoadRounds(slot, mount, k) > 0 ? k : NextLoad(slot, mount);
    }

    /// <summary>Rounds of a mount's load go (the dev's salvo spends them without a reload).</summary>
    public void Spend(int slot, int mount, int load, int guns)
    {
        ref var s = ref mounts[slot]![mount];
        ref int left = ref loadRounds[slot]![loadouts[slot]!.MountLoadStart(mount) + load];
        int spent = left == int.MaxValue ? guns : Math.Min(left, guns);
        if (left != int.MaxValue)
            left -= spent;
        s.Rounds = s.Rounds == int.MaxValue ? s.Rounds : Math.Max(s.Rounds - spent, 0);
        Touched = true;
    }

    /// <summary>A mount's rounds left of its battery's load k (of the ship's loadout).</summary>
    public int LoadRounds(int slot, int mount, int k) => loadRounds[slot]![loadouts[slot]!.MountLoadStart(mount) + k];

    /// <summary>The load a mount fires next: the first of its battery's loads (in the magazine order) it has rounds of, or
    /// -1 when it has none left.</summary>
    public int NextLoad(int slot, int mount)
    {
        var lo = loadouts[slot]!;
        int s0 = lo.MountLoadStart(mount), n = lo.MountLoadStart(mount + 1) - s0;
        var r = loadRounds[slot]!;
        for (int k = 0; k < n; k++)
            if (r[s0 + k] > 0)
                return k;
        return -1;
    }

    /// <summary>The load of a kind a mount has rounds of (the first such in the order), or -1.</summary>
    public int LoadOf(int slot, int mount, ShellKind kind)
    {
        var lo = loadouts[slot]!;
        ref readonly var m = ref lo.Class.Mounts[mount];
        if (m.Battery < 0)
            return -1;
        var loads = lo.Loads(m.Battery);
        int s0 = lo.MountLoadStart(mount);
        var r = loadRounds[slot]!;
        for (int k = 0; k < loads.Length; k++)
            if (lo.ShellTypes[loads[k].ShellType].Kind == kind && r[s0 + k] > 0)
                return k;
        return -1;
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
    public static bool InArcs(ShipClass cls, in MountRow m, double bearingDeg) => InArcs(cls, m.Arcs, bearingDeg);

    /// <summary>Whether a relative bearing is in arcs (pairs of degrees clockwise from ahead, the class's Numbers; none:
    /// all round).</summary>
    public static bool InArcs(ShipClass cls, Slice arcs, double bearingDeg)
    {
        var a = cls.Numbers(arcs);
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
        // the loadouts given (only those, so a world of the designs' own hashes as before loadouts)
        for (int slot = 0; slot < store.Slots; slot++)
            if (store.IsAlive(slot) && !loadouts[slot]!.IsDefault)
            {
                h.Add(slot);
                h.Add(loadouts[slot]!.Hash.A);
                h.Add(loadouts[slot]!.Hash.B);
            }
        if (!Touched)
            return;
        for (int slot = 0; slot < store.Slots; slot++)
        {
            bool orders = ordered[slot] is { } os && os.AsSpan().ContainsAnyExcept((byte)0);
            if (!store.IsAlive(slot) || !Moved(slot) && engaged[slot] == 0 && !orders)
                continue;
            h.Add(slot);
            h.Add(engaged[slot]);
            if (orders)
                foreach (byte k in ordered[slot].AsSpan())
                    h.Add(k);
            foreach (ref readonly var s in mounts[slot].AsSpan())
            {
                h.Add(s.TrainMs);
                h.Add(s.TrainFrom);
                h.Add(s.TrainTo);
                h.Add(s.ElevTo);
                h.Add(s.LoadedMs);
                h.Add(s.Rounds);
                h.Add(s.Ready);
                if (s.Stiff != 0)
                    h.Add(s.Stiff);
                if (s.InGuns != 0)
                    h.Add(s.InGuns);
            }
            foreach (int r in loadRounds[slot].AsSpan())
                h.Add(r);
            foreach (ref readonly var l in launchers[slot].AsSpan())
            {
                h.Add(l.Loaded);
                h.Add(l.Spare);
                h.Add(l.LoadedMs);
                h.Add(l.FiredMs);
            }
        }
    }
}
