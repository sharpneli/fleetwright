using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim;

/// <summary>Star shell in the fight (docs/battle/night.md Q5): a ship's illuminating task, its star shells in flight and
/// their flares burning.
/// <list type="bullet">
/// <item><b>The task:</b> a ship ordered to light a contact (<see cref="Illuminate"/>) gives one mount the job: one of her
/// working mounts with star rounds that bears on her, of the lightest battery that has them (the light guns'). That mount
/// leaves its battery's salvos (<see cref="CombatStore.Illuminating"/>); the rest fire at what it shows. While the task
/// stands it keeps a flare up: the next shell goes so that it bursts as the last has <see cref="SimSettings.StarOverlapS"/>
/// left to burn, once the mount is loaded and laid.</item>
/// <item><b>The aim:</b> where her navy's picture has the contact when the shell would burst, then
/// <see cref="SimSettings.StarBeyondM"/> on along the line of fire, <see cref="SimSettings.StarBurstM"/> up: so she stands
/// dark against the sea it lights, for the firer. The gun is laid so its shell passes through that height there (the
/// range table's flight, by bisection on the elevation: on its way up close in), the time fuze set to that moment.</item>
/// <item><b>The errors:</b> the laying's (<see cref="SimSettings.StarDispersionMil"/> in elevation and train) and the time
/// fuze's (its block's scatter, a share of the time set); a share of the fuzes fail (its blind share): no flare.</item>
/// <item><b>A flare</b> bursts at its time and place, burns for its candle's time at its light, falls at its parachute's
/// rate and drifts with the wind; out when burnt or in the sea. Its light is nobody's (the firer shows only by her gun's
/// flash): it lights what's near for every eye, glares in the eyes that look near it, and lights the sea behind a ship
/// between it and the eye (<see cref="Lights.BacklightCdM2"/>).</item>
/// </list>
/// A shell's flight to its burst is the range table's and draws nothing in the world (no splash, no hit: it bursts in the
/// air). A tick on a lattice of world time while any task stands (any tier). World state: replays and hashes. Always
/// registered ninth (<see cref="Id"/>).</summary>
public sealed class StarShellSystem : IWorldSystem
{
    public const byte Id = 9;
    const ushort IlluminateCode = 1, TickEvent = 1;

    /// <summary>A ship's illuminating task: whose (an entity), on whom (an entity), the mount (-1: none found yet) and its
    /// battery, and when its newest flare bursts and burns out (0: none yet).</summary>
    struct Task
    {
        public int Ship, Target, Mount, Battery;
        public long BurstMs, OutMs;
        /// <summary>The last lay's elevation and time of flight (0: not laid yet).</summary>
        public double Elev, Tof;
    }

    /// <summary>A flare: where it bursts (its height in the place) and when, its light, how long it burns and how fast it
    /// falls, the wind's drift (m/s, along the sea), and whose task fired it (an entity).</summary>
    struct Flare
    {
        public Vec3d At, Drift;
        public long BurstMs;
        public double Cd, BurnS, FallMs;
        public int Ship;
    }

    readonly SimSettings settings;
    readonly ShipSystem ships;
    readonly ShellSystem shells;
    readonly RandomStream stream;
    Task[] tasks = new Task[8];
    int taskCount;
    Flare[] flares = new Flare[16];
    int flareCount;
    bool ticking;
    long fired;   // star shells fired, for the draws

    internal StarShellSystem(SimSettings settings, ShipSystem ships, ShellSystem shells, ulong seed)
    {
        (this.settings, this.ships, this.shells) = (settings, ships, shells);
        stream = RandomStream.Named(seed, "star shell");
    }

    /// <summary>A ship lights a contact (an entity) with star shell, or stops (0).</summary>
    public static Command Illuminate(int ship, int target) => new(Id, IlluminateCode, ship, A: target);

    /// <summary>The contact a ship is lighting with star shell (0: none), and her illuminating mount (-1: none).</summary>
    public (int Target, int Mount) TaskOf(int ship)
    {
        int i = Find(ship);
        return i < 0 ? (0, -1) : (tasks[i].Target, tasks[i].Mount);
    }

    /// <summary>The flares burning now: how many, and each one's place (its height in it) and light; for tools, tests and the
    /// map's placeholder.</summary>
    public int FlareCount => flareCount;

    public (Vec3d At, double Cd) FlareAt(int i, long nowMs) => (Where(flares[i], nowMs), flares[i].Cd);

    public void Apply(World world, in Command c)
    {
        if (c.Code != IlluminateCode)
            throw new ArgumentException($"star shell has no command {c.Code}");
        int i = Find(c.Entity);
        int target = (int)c.A;
        if (target == 0)
        {
            if (i >= 0)
                Drop(i);
            return;
        }
        if (i >= 0 && tasks[i].Target == target)
            return;   // as it is
        if (i >= 0)
            Drop(i);
        i = ~Find(c.Entity);
        if (taskCount == tasks.Length)
            Array.Resize(ref tasks, 2 * taskCount);
        Array.Copy(tasks, i, tasks, i + 1, taskCount - i);
        taskCount++;
        tasks[i] = new Task { Ship = c.Entity, Target = target, Mount = -1, Battery = -1 };
        Tick(world, schedule: true);
    }

    int Find(int ship)
    {
        int lo = 0, hi = taskCount - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            if (tasks[mid].Ship == ship)
                return mid;
            if (tasks[mid].Ship < ship)
                lo = mid + 1;
            else
                hi = mid - 1;
        }
        return ~lo;
    }

    /// <summary>A task ended: its mount back to its battery's salvos.</summary>
    void Drop(int i)
    {
        ref readonly var t = ref tasks[i];
        if (t.Mount >= 0 && ships.Store.SlotOf(t.Ship) is >= 0 and var slot)
            ships.Store.Combat.SetIlluminating(slot, t.Mount, false);
        Array.Copy(tasks, i + 1, tasks, i, taskCount - i - 1);
        taskCount--;
    }

    public void Handle(World world, in SimEvent e)
    {
        if (e.Code != TickEvent)
            throw new ArgumentException($"star shell has no event {e.Code}");
        ticking = false;
        Tick(world, schedule: false);
    }

    void Tick(World world, bool schedule)
    {
        long now = world.Time.Ms, step = settings.StarCheckMs;
        if (!schedule)
        {
            Prune(now);
            for (int i = taskCount - 1; i >= 0; i--)
                if (!Work(world, i, now))
                    Drop(i);
        }
        if (taskCount > 0 && !ticking)
        {
            long next = schedule ? (now + step - 1) / step * step : (now / step + 1) * step;
            world.Schedule(new WorldTime(Math.Max(next, now)), EventTier.Fine, 0, Id, TickEvent);
            ticking = true;
        }
    }

    /// <summary>Flares burnt out or in the sea go.</summary>
    void Prune(long now)
    {
        int n = 0;
        for (int i = 0; i < flareCount; i++)
            if (now < OutMs(flares[i]))
                flares[n++] = flares[i];
        flareCount = n;
    }

    static long OutMs(in Flare f) =>
        f.BurstMs + (long)(1000 * Math.Min(f.BurnS, f.FallMs > 0 ? (f.At.Length - Earth.Radius) / f.FallMs : f.BurnS));

    static Vec3d Where(in Flare f, long nowMs)
    {
        double t = Math.Max(nowMs - f.BurstMs, 0) / 1000.0;
        var up = f.At.Normalized();
        return f.At + f.Drift * t - up * (f.FallMs * t);
    }

    /// <summary>One task's tick: false when it ends (her ship or the contact gone, the contact seen going down, no mount
    /// left to fire star shell at her); while her navy doesn't hold the contact it waits. Else a shell, when one's due and
    /// the mount is ready.</summary>
    bool Work(World world, int i, long now)
    {
        ref var t = ref tasks[i];
        var store = ships.Store;
        int slot = store.SlotOf(t.Ship), tslot = store.SlotOf(t.Target);
        if (slot < 0 || tslot < 0)
            return false;
        var ship = store[slot];
        var pic = world.Sightings.Of(ship.Side);
        int ci = pic?.IndexOf(t.Target) ?? -1;
        if (ci < 0)
            return true;   // not held (yet): nothing to aim at, the task waits
        if (pic![ci].Lost)
            return false;
        ref readonly var c = ref pic[ci];
        var cls = ship.Handling.Class;
        var combat = store.Combat;
        var cond = combat.Condition(slot);
        double brg = Earth.Bearing(ship.Position, c.Position), rel = brg - ship.HeadingDeg;
        // the mount: kept while it works, has star rounds and bears; else the lightest battery's first that does
        if (t.Mount < 0 || !cond.MountWorks[t.Mount] || combat.LoadOf(slot, t.Mount, ShellKind.Star) < 0 || !CombatStore.InArcs(cls, cls.Mounts[t.Mount], rel))
        {
            if (t.Mount >= 0)
                combat.SetIlluminating(slot, t.Mount, false);
            (t.Mount, t.Battery) = (-1, -1);
            double lightest = double.MaxValue;
            for (int m = 0; m < cls.Mounts.Length; m++)
            {
                ref readonly var mr = ref cls.Mounts[m];
                if (mr.Battery < 0 || !cond.MountWorks[m] || combat.LoadOf(slot, m, ShellKind.Star) < 0 || !CombatStore.InArcs(cls, mr, rel))
                    continue;
                double cal = cls.Batteries[mr.Battery].CalibreMm;
                if (cal < lightest)
                    (lightest, t.Mount, t.Battery) = (cal, m, mr.Battery);
            }
            if (t.Mount < 0)
                return false;
            combat.SetIlluminating(slot, t.Mount, true);
        }
        var lo = combat.Loadout(slot);
        int load = combat.LoadOf(slot, t.Mount, ShellKind.Star);
        ref readonly var row = ref cls.Mounts[t.Mount];
        var type = lo.ShellTypes[lo.Loads(t.Battery)[load].ShellType];
        var table = shells.Ballistics[shells.TableOf(lo, t.Battery, lo.Loads(t.Battery)[load].ShellType)];
        // where she'll be: the picture's estimate carried on, a look's flight on (the first guess), then beyond and up
        var frame = ship.Frame;
        var muzzle = Muzzle(cls, frame, t.Mount);
        double h0 = muzzle.Length - Earth.Radius;
        // between shells the mount follows her bearing at its last elevation; the lay is worked out only as one comes due
        if (t.Tof > 0 && now + (long)(1000 * (t.Tof - settings.StarOverlapS + LayAheadS)) < t.OutMs)
        {
            var est = c.Position + c.Velocity * ((now - c.EstMs) / 1000.0);
            combat.Order(slot, t.Mount, row, now, Earth.Bearing(muzzle, est) - ship.HeadingDeg, t.Elev);
            return true;
        }
        if (!Lay(table, c, muzzle, h0, now, out double elev, out double tof, out var forward))
            return true;   // beyond the star's reach for now: kept, she may close
        (t.Elev, t.Tof) = (elev, tof);
        double bearing = Earth.Heading(muzzle, forward) - ship.HeadingDeg;
        combat.Order(slot, t.Mount, row, now, bearing, elev);
        // due: none up yet, or the last burns out within this flight and the overlap
        bool due = t.OutMs == 0 || now + (long)(1000 * (tof - settings.StarOverlapS)) >= t.OutMs;
        ref readonly var ms = ref combat.Mount(slot, t.Mount);
        // (laid within the check: the order above re-anchors it each second, following her)
        if (!due || ms.LoadedMs > now || CombatStore.LaidBy(ms, row) > now + settings.StarCheckMs || cond.Guns(t.Mount) <= 0)
            return true;
        // fire one gun: its flash, its round, its reload
        combat.Fired(slot, cls, t.Mount, load, 1, now);
        var r = stream.Derive((ulong)fired++);
        double mil = settings.StarDispersionMil / 1000;
        double e = elev + r.NextGaussian() * mil * 180 / Math.PI;
        var f = Earth.Turn(muzzle, forward, r.NextGaussian() * mil);
        double tf = tof * (1 + type.FuzeScatter * r.NextGaussian());
        long burstMs = now + (long)(1000 * Math.Max(tf, 0.5));
        t.BurstMs = burstMs;
        t.OutMs = burstMs + (long)(1000 * type.StarBurnS);
        if (r.NextDouble() < type.FuzeBlind || type.StarCd <= 0)
            return true;   // a dud: the next goes when this one would have burnt out
        var at = table.Position(muzzle, f, e, 1, tf, out _);
        if (at.Length - Earth.Radius <= 0)
            return true;   // the fuze set long: into the sea
        var w = world.Weather.At(at, world.Time);
        var drift = Earth.Forward(at, w.WindFromDeg + 180) * w.WindMs;
        if (flareCount == flares.Length)
            Array.Resize(ref flares, 2 * flareCount);
        flares[flareCount++] = new Flare { At = at, Drift = drift, BurstMs = burstMs, Cd = type.StarCd, BurnS = type.StarBurnS, FallMs = type.StarFallMs, Ship = t.Ship };
        return true;
    }

    /// <summary>How long before a shell's due its lay is worked out, s: time for the mount to lay.</summary>
    const double LayAheadS = 5;

    /// <summary>The gun laid for a star shell at a contact: her picture's estimate carried on to the burst, then
    /// <see cref="SimSettings.StarBeyondM"/> on along the line of fire and <see cref="SimSettings.StarBurstM"/> up; the elevation
    /// whose flight passes through that height there (on its way up close in, as a flat gun's must, on its way down further
    /// out: bisection between the one that throws to it along the sea and the gun's highest), the time of flight to it and
    /// the line of fire. False beyond reach.</summary>
    bool Lay(BallisticTable table, in Contact c, Vec3d muzzle, double h0, long now, out double elev, out double tof, out Vec3d forward)
    {
        (elev, tof, forward) = (double.NaN, double.NaN, default);
        double lead = 0;
        for (int pass = 0; pass < 2; pass++)
        {
            var est = c.Position + c.Velocity * ((now - c.EstMs) / 1000.0 + lead);
            double d = Earth.Distance(muzzle, est) + settings.StarBeyondM;
            forward = Earth.Forward(muzzle, Earth.Bearing(muzzle, est));
            double lo = table.ElevationFor(d, h0), hi = Math.Min(table.Key.MaxElevationDeg, table.MaxElevationDeg - 2 * BallisticTable.ElevationStep);
            if (double.IsNaN(lo) || !(HeightOver(table, hi, h0, d, out _) >= settings.StarBurstM))
                return false;   // even its highest passes under the height there
            for (int k = 0; k < 40 && hi - lo > 1e-3; k++)
            {
                double mid = 0.5 * (lo + hi);
                (lo, hi) = HeightOver(table, mid, h0, d, out _) >= settings.StarBurstM ? (lo, mid) : (mid, hi);
            }
            elev = hi;
            HeightOver(table, elev, h0, d, out tof);
            if (!(tof <= settings.StarFuzeMaxS))
                return false;   // past the time fuze's longest setting
            lead = tof;
        }
        return true;
    }

    /// <summary>How high over the sea a flight is when it's a distance along it (m), and when (s); -1 if it never gets that far.</summary>
    static double HeightOver(BallisticTable table, double elev, double h0, double distanceM, out double t)
    {
        double density = BallisticTable.DensityAt(h0), t0 = 0, t1 = table.Fall(elev, h0).Tof;
        t = double.NaN;
        if (!(t1 > 0) || table.At(elev, 1, density, t1).S < distanceM)
            return -1;
        for (int k = 0; k < 40 && t1 - t0 > 1e-3; k++)
        {
            double mid = 0.5 * (t0 + t1);
            (t0, t1) = table.At(elev, 1, density, mid).S >= distanceM ? (t0, mid) : (mid, t1);
        }
        t = t1;
        return h0 + table.At(elev, 1, density, t).H;
    }

    // each star shell's table's reach (by the table's index): the furthest its gun's highest elevation passes the burst's
    // height, less the burst's distance beyond
    readonly Dictionary<int, double> reaches = [];

    /// <summary>How far a ship can light a contact with star shell now, m: the furthest of her working mounts with star
    /// rounds can burst one beyond her (0: none).</summary>
    public double ReachM(int ship)
    {
        var store = ships.Store;
        if (store.SlotOf(ship) is not (>= 0 and var slot))
            return 0;
        var cls = store[slot].Handling.Class;
        var combat = store.Combat;
        var cond = combat.Condition(slot);
        var lo = combat.Loadout(slot);
        double best = 0;
        for (int m = 0; m < cls.Mounts.Length; m++)
        {
            int b = cls.Mounts[m].Battery, load = b >= 0 && cond.MountWorks[m] ? combat.LoadOf(slot, m, ShellKind.Star) : -1;
            if (load < 0)
                continue;
            int ti = shells.TableOf(lo, b, lo.Loads(b)[load].ShellType);
            if (!reaches.TryGetValue(ti, out double r))
            {
                var table = shells.Ballistics[ti];
                double hi = Math.Min(table.Key.MaxElevationDeg, table.MaxElevationDeg - 2 * BallisticTable.ElevationStep), d0 = 0, d1 = table.Fall(hi, 10).Range;
                for (int k = 0; k < 40 && d1 - d0 > 10; k++)
                {
                    double mid = 0.5 * (d0 + d1);
                    (d0, d1) = HeightOver(table, hi, 10, mid, out double tm) >= settings.StarBurstM && tm <= settings.StarFuzeMaxS ? (mid, d1) : (d0, mid);
                }
                reaches[ti] = r = Math.Max(d0 - settings.StarBeyondM, 0);
            }
            best = Math.Max(best, r);
        }
        return best;
    }

    /// <summary>A mount's muzzle in the world (the gunnery's own rule: its guns' axis over the sea).</summary>
    static Vec3d Muzzle(ShipClass cls, in ShipFrame frame, int mount)
    {
        ref readonly var m = ref cls.Mounts[mount];
        ref readonly var comp = ref cls.Components[m.Component];
        double axis = comp.Bounds.Base + 0.55 * (comp.Bounds.Top - comp.Bounds.Base);
        return frame.ToWorld(m.X, m.Y, axis - cls.Vertical.Waterline);
    }

    /// <summary>The flares burning now into the lights (at a look).</summary>
    internal void Gather(World world, Lights lights)
    {
        long now = world.Time.Ms;
        for (int i = 0; i < flareCount; i++)
        {
            ref readonly var f = ref flares[i];
            if (now >= f.BurstMs && now < OutMs(f))
                lights.AddFlare(Where(f, now), f.Cd);
        }
    }

    public void Hash(ref StateHash h)
    {
        if (taskCount == 0 && flareCount == 0 && fired == 0)
            return;
        h.Add(taskCount);
        for (int i = 0; i < taskCount; i++)
        {
            ref readonly var t = ref tasks[i];
            h.Add(t.Ship);
            h.Add(t.Target);
            h.Add(t.Mount);
            h.Add(t.Battery);
            h.Add(t.BurstMs);
            h.Add(t.OutMs);
        }
        h.Add(flareCount);
        for (int i = 0; i < flareCount; i++)
        {
            ref readonly var f = ref flares[i];
            h.Add(f.At);
            h.Add(f.Drift);
            h.Add(f.BurstMs);
            h.Add(f.Cd);
            h.Add(f.BurnS);
            h.Add(f.FallMs);
            h.Add(f.Ship);
        }
        h.Add(fired);
        h.Add(ticking);
    }

    /// <summary>The flares burning, for the map's placeholder (docs/battle/night.md Q10: a disc a flare); a flare is
    /// seen by everyone.</summary>
    public void Fill(Snapshot s)
    {
        var v = s.Flares;
        v.EnsureCapacity(flareCount);
        int n = 0;
        long now = s.Time.Ms;
        for (int i = 0; i < flareCount; i++)
        {
            ref readonly var f = ref flares[i];
            if (now < f.BurstMs || now >= OutMs(f))
                continue;
            (v.At[n], v.Candela[n]) = (Where(f, now), f.Cd);
            n++;
        }
        v.Count = n;
    }
}
