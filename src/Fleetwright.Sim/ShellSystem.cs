using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim;

/// <summary>What the shell system understands (<see cref="ShellSystem"/>).</summary>
public enum ShellCommand : ushort
{
    FireAt = 1,
}

/// <summary>Shells in flight and their splashes (docs/battle/fighting.md 4.2a, "A salvo's life, in events"). A shell is a
/// few numbers, its whole flight known from its ballistic table the moment it's fired: the fire time, the muzzle, the
/// heading of its plane, the elevation and muzzle velocity it left with (the ship's own motion folded in). It costs
/// nothing until two events:
/// <list type="bullet">
/// <item>its danger window, when it comes down through <see cref="SimSettings.ShellWindowTopM"/>: its track from there
/// to the water (and <see cref="SimSettings.ShellUnderwaterM"/> on under it), in chords, goes through the hit query
/// against the ships the world's index finds near the fall, each where it is then; the first ship it meets is hit;</item>
/// <item>its end, the hit or the water: a splash (or a hit's mark) goes into the ring the screens read, and the shell's
/// record is freed.</item>
/// </list>
/// Shells don't depend on the clock's tier or tick: one fired lands where it lands. Records and splashes are pooled.
/// <para>The world registers it third, so its id is always <see cref="Id"/>.</para></summary>
public sealed class ShellSystem : IWorldSystem
{
    public const byte Id = 3;
    const ushort WindowEvent = 1, EndEvent = 2;
    const int Chords = 4;

    /// <summary>A shell in flight.</summary>
    public struct Shell
    {
        public int Table;
        public long FiredMs;
        public Vec3d Muzzle, Forward;
        public double ElevationDeg, Speed;
        /// <summary>Seconds after firing: the window's top, and the end (the hit, or the water until the window's run).</summary>
        public double WindowS, EndS;
        public int Salvo, Shooter, Battery, Generation;
        /// <summary>The gunnery system's record of its salvo (for the spot), or -1 (the dev's fire); the shell type
        /// (the shooter's class's).</summary>
        public int Record, ShellType;
        public bool Alive;
        /// <summary>The ship it hits (an entity), or -1; and where it ends.</summary>
        public int Hit;
        public Vec3d End;
    }

    /// <summary>A splash, or a hit's mark (Ship: the ship hit, an entity), at a moment.</summary>
    public readonly record struct Splash(Vec3d Point, long Ms, int Ship, int Salvo, int Shooter);

    readonly SimSettings settings;
    readonly ShipSystem ships;
    readonly ShipIndex index;
    readonly CombatLog log;
    readonly HitPath path = new();
    Shell[] shells;
    int[] free;
    int freeCount, high, alive;
    readonly Splash[] splashes;
    int splashNext, splashCount;
    int nextSalvo;
    bool fired;
    double biggest;   // the biggest class's reach in the world (ShipSolids.Radius)
    int[] candidates = new int[16];
    readonly Vec3d[] chordPoint = new Vec3d[Chords + 2];
    readonly double[] chordTime = new double[Chords + 2];

    /// <summary>The world's ballistic tables (the renderer looks shells up in them, by the snapshot's table index).</summary>
    public BallisticsRegistry Ballistics { get; } = new();

    /// <summary>Shells in flight now; hits and misses so far.</summary>
    public int InFlight => alive;
    public int Hits { get; private set; }
    public int Misses { get; private set; }

    /// <summary>Told of each shell of a gunnery salvo as it comes down (set by the world).</summary>
    internal GunnerySystem? Gunnery { get; set; }

    internal ShellSystem(SimSettings settings, ShipSystem ships, ShipIndex index, CombatLog log)
    {
        (this.settings, this.ships, this.index, this.log) = (settings, ships, index, log);
        shells = new Shell[Math.Max(settings.ShellCapacity, 16)];
        free = new int[shells.Length];
        splashes = new Splash[Math.Max(settings.SplashCapacity, 16)];
    }

    /// <summary>A shell's record by its slot (the snapshot's), for tests and tools.</summary>
    public ref readonly Shell this[int slot] => ref shells[slot];
    public int Slots => high;

    // ------------------------------------------------------------------ commands

    /// <summary>The dev's "fire at" (until fire control, 4.2b): a ship's battery fires a salvo at a point on the sea, each
    /// gun's elevation from the range table, each shell with its own error (<see cref="SimSettings.DevFirePatternPct"/>),
    /// drawn from the stream "fire" by salvo and shell.</summary>
    public static Command FireAt(int shooter, int battery, double latDeg, double lonDeg) =>
        new(Id, (ushort)ShellCommand.FireAt, shooter, A: Earth.PackLatLon(latDeg, lonDeg), B: battery);

    public void Apply(World world, in Command c)
    {
        if ((ShellCommand)c.Code != ShellCommand.FireAt)
            return;
        var store = ships.Store;
        int slot = store.SlotOf(c.Entity);
        if (slot < 0)
            return;
        var ship = store[slot];
        var cls = ships.Class(ship.ClassIndex);
        if (c.B < 0 || c.B >= cls.Batteries.Length)
            return;
        var (lat, lon) = Earth.UnpackLatLon(c.A);
        FireBattery(world, slot, cls, c.B, Earth.Position(lat, lon));
    }

    /// <summary>The tables for a class's batteries, and the hit query's room for its solids, made the first time it's in
    /// the world (not at its first salvo or near miss).</summary>
    internal void Prepare(ShipClass cls)
    {
        biggest = Math.Max(biggest, cls.Solids.Radius);
        path.Reserve(cls.Solids.Solids.Length);   // the hit query's room for it, now rather than at its first near miss
        foreach (ref readonly var b in cls.Batteries)
            Ballistics.Ensure(KeyOf(cls, b));
    }

    static BallisticKey KeyOf(ShipClass cls, in BatteryRow b) =>
        BallisticKey.Of(cls.ShellTypes[b.Fires], cls.Mounts[cls.Links(b.Mounts)[0]].MaxElevationDeg);

    /// <summary>The index of a class's battery's table in <see cref="Ballistics"/> (built now if it's new).</summary>
    public int TableOf(ShipClass cls, int battery) => Ballistics.Ensure(KeyOf(cls, cls.Batteries[battery]));

    /// <summary>A new salvo's number.</summary>
    public int NewSalvo() => nextSalvo++;

    void FireBattery(World world, int slot, ShipClass cls, int battery, Vec3d target)
    {
        var ship = ships.Store[slot];
        ref readonly var b = ref cls.Batteries[battery];
        int table = Ballistics.Ensure(KeyOf(cls, b));
        var t = Ballistics[table];
        var frame = ship.Frame;
        var shipVelocity = ship.Forward * ship.Speed;
        double v0 = t.Key.MuzzleVelocityMs, wl = cls.Vertical.Waterline;
        int salvo = nextSalvo++;
        var stream = world.Stream("fire").Derive((ulong)salvo);
        int k = 0;
        foreach (int m in cls.Links(b.Mounts))
        {
            ref readonly var mount = ref cls.Mounts[m];
            ref readonly var comp = ref cls.Components[mount.Component];
            // the guns' axis, as Armament.BarrelBand has it, over the sea
            double axis = comp.Bounds.Base + 0.55 * (comp.Bounds.Top - comp.Bounds.Base);
            var muzzle = frame.ToWorld(mount.X, mount.Y, axis - wl);
            var up = muzzle.Normalized();
            var toward = (target - up * Vec3d.Dot(target, up)).Normalized();
            double range = Earth.Distance(muzzle, target), h0 = muzzle.Length - Earth.Radius;
            for (int g = 0; g < cls.TurretTypes[mount.Type].Barrels; g++, k++)
            {
                var rng = stream.Derive((ulong)k);
                double sigma = settings.DevFirePatternPct / 100 * range;
                double aim = range + rng.NextGaussian() * sigma, across = rng.NextGaussian() * sigma * settings.DevFireDeflectionShare / Math.Max(range, 1);
                double e = t.ElevationFor(aim, h0);
                if (double.IsNaN(e))
                    e = Math.Min(t.Key.MaxElevationDeg, t.MaxElevationDeg - 2 * BallisticTable.ElevationStep);
                var fwd = toward * Math.Cos(across) + Vec3d.Cross(up, toward) * Math.Sin(across);
                // the ship's own motion folded in: the shell leaves with the gun's velocity and the ship's
                double er = double.DegreesToRadians(e);
                var v = (fwd * Math.Cos(er) + up * Math.Sin(er)) * v0 + shipVelocity;
                double sp = v.Length, vu = Vec3d.Dot(v, up);
                Launch(world, table, muzzle, (v - up * vu).Normalized(), double.RadiansToDegrees(Math.Asin(vu / sp)), sp / v0, salvo, ship.Entity,
                    battery, -1, b.Fires);
            }
        }
    }

    /// <summary>A shell away: its record, its window and end solved, its window's event scheduled. False if it never
    /// comes down (no shell is made).</summary>
    public bool Launch(World world, int table, Vec3d muzzle, Vec3d forward, double elevationDeg, double speed, int salvo, int shooter, int battery,
        int record = -1, int shellType = -1)
    {
        var t = Ballistics[table];
        double end = t.Reaches(muzzle, forward, elevationDeg, speed, 0);
        if (double.IsNaN(end))
            return false;
        double top = t.Reaches(muzzle, forward, elevationDeg, speed, settings.ShellWindowTopM);
        if (double.IsNaN(top) || top > end)
            top = 0;   // it never rises over the window's top: all its flight is in the window
        int i = Allocate();
        ref var s = ref shells[i];
        int gen = s.Generation + 1;
        s = new Shell
        {
            Table = table, FiredMs = world.Time.Ms, Muzzle = muzzle, Forward = forward, ElevationDeg = elevationDeg, Speed = speed, WindowS = top,
            EndS = end, Salvo = salvo, Shooter = shooter, Battery = battery, Record = record, ShellType = shellType, Generation = gen, Alive = true,
            Hit = -1,
        };
        alive++;
        fired = true;
        world.Schedule(new WorldTime(s.FiredMs + (long)Math.Floor(top * 1000)), EventTier.Fine, i, Id, WindowEvent, gen);
        return true;
    }

    int Allocate()
    {
        if (freeCount > 0)
            return free[--freeCount];
        if (high == shells.Length)
        {
            Array.Resize(ref shells, shells.Length * 2);
            Array.Resize(ref free, shells.Length);
        }
        return high++;
    }

    // ------------------------------------------------------------------ events

    public void Handle(World world, in SimEvent e)
    {
        ref var s = ref shells[e.Entity];
        if (!s.Alive || s.Generation != e.Generation)
            return;
        if (e.Code == WindowEvent)
            Window(world, ref s, e.Entity);
        else if (e.Code == EndEvent)
        {
            splashes[splashNext] = new Splash(s.End, world.Time.Ms, s.Hit, s.Salvo, s.Shooter);
            splashNext = (splashNext + 1) % splashes.Length;
            splashCount = Math.Min(splashCount + 1, splashes.Length);
            if (s.Hit >= 0)
            {
                Hits++;
                log.Add(new CombatEntry(world.Time.Ms, CombatEvent.Hit, s.Shooter, s.Battery, s.Hit, s.ShellType));
            }
            else
                Misses++;
            s.Alive = false;
            alive--;
            free[freeCount++] = e.Entity;
            if (s.Record >= 0)
                Gunnery?.Landed(world, s.Record, s.End, s.Hit);
        }
    }

    /// <summary>The danger window: the track from its top to the water, and on under it, in chords through the hit query
    /// against the ships near the fall, each where it is at the chord's middle; the first ship met is hit.</summary>
    void Window(World world, ref Shell s, int slot)
    {
        var t = Ballistics[s.Table];
        Vec3d vel = default;
        for (int j = 0; j <= Chords; j++)
        {
            chordTime[j] = s.WindowS + (s.EndS - s.WindowS) * j / Chords;
            chordPoint[j] = t.Position(s.Muzzle, s.Forward, s.ElevationDeg, s.Speed, chordTime[j], out vel);
        }
        double speed = vel.Length;
        chordPoint[Chords + 1] = chordPoint[Chords] + vel * (settings.ShellUnderwaterM / speed);
        chordTime[Chords + 1] = s.EndS + settings.ShellUnderwaterM / speed;
        (s.End, s.Hit) = (chordPoint[Chords], -1);

        // the ships that can be under it: within the window's span and the biggest ship's reach of the fall
        var near = index.Near(chordPoint[Chords], (chordPoint[0] - chordPoint[Chords]).Length + biggest);
        if (near.Length > 0)
        {
            if (candidates.Length < near.Length)
                candidates = new int[Math.Max(near.Length, candidates.Length * 2)];
            near.CopyTo(candidates);
            int n = near.Length;
            var store = ships.Store;
            for (int j = 0; j <= Chords && s.Hit < 0; j++)
            {
                double bestT = double.PositiveInfinity;
                for (int c = 0; c < n; c++)
                {
                    int ship = candidates[c];
                    var cls = ships.Class(store[ship].ClassIndex);
                    long midMs = s.FiredMs + (long)Math.Round((chordTime[j] + chordTime[j + 1]) / 2 * 1000);
                    var frame = FrameAt(ship, midMs);
                    if (!HitQuery.Trace(cls, frame, Attitude.Upright, chordPoint[j], chordPoint[j + 1], [], path))
                        continue;
                    foreach (ref readonly var x in path.Crossings)
                        if (x.Kind is CrossingKind.HullIn or CrossingKind.PartIn)
                        {
                            if (x.T < bestT)
                            {
                                bestT = x.T;
                                s.Hit = store[ship].Entity;
                                s.End = HitQuery.ToWorld(cls, frame, Attitude.Upright, x.Point);
                                s.EndS = chordTime[j] + (chordTime[j + 1] - chordTime[j]) * x.T / Math.Max(path.Length, 1e-9);
                            }
                            break;
                        }
                }
            }
        }
        world.Schedule(new WorldTime(Math.Max(world.Time.Ms, s.FiredMs + (long)Math.Floor(s.EndS * 1000))), EventTier.Fine, slot, Id, EndEvent, s.Generation);
    }

    /// <summary>A ship's frame at a moment near now: on rails or coarse, its own; on the fine tick, its pose carried on
    /// along its course at its speed (the window is a fraction of a second).</summary>
    ShipFrame FrameAt(int slot, long ms)
    {
        var store = ships.Store;
        store.PoseAt(slot, ms, out var p, out var f);
        var ship = store[slot];
        if (ship.Tier == ClockTier.Fine)
        {
            double d = ship.Speed * (ms - ship.PoseMs) / 1000;
            p = (p + f * d).Normalized() * Earth.Radius;
            var up = p.Normalized();
            f = (f - up * Vec3d.Dot(f, up)).Normalized();
        }
        return ShipFrame.At(p, f);
    }

    // ------------------------------------------------------------------ the rest

    public void Hash(ref StateHash h)
    {
        if (!fired)
            return;   // a world that never fired hashes as it did before there were shells
        h.Add(alive);
        h.Add(nextSalvo);
        for (int i = 0; i < high; i++)
            if (shells[i].Alive)
            {
                ref readonly var s = ref shells[i];
                h.Add(i);
                h.Add(s.Table);
                h.Add(s.FiredMs);
                h.Add(s.ElevationDeg);
                h.Add(s.Speed);
                h.Add(s.EndS);
                h.Add(s.Hit);
            }
        h.Add(Hits);
        h.Add(Misses);
    }

    public void Fill(Snapshot snap)
    {
        var v = snap.Shells;
        v.EnsureCapacity(alive);
        int n = 0;
        for (int i = 0; i < high; i++)
            if (shells[i].Alive)
            {
                ref readonly var s = ref shells[i];
                (v.Table[n], v.FiredMs[n], v.Muzzle[n], v.Forward[n], v.ElevationDeg[n], v.Speed[n], v.EndS[n], v.Hit[n], v.Shooter[n]) =
                    (s.Table, s.FiredMs, s.Muzzle, s.Forward, s.ElevationDeg, s.Speed, s.EndS, s.Hit, s.Shooter);
                n++;
            }
        v.Count = n;
        v.Ballistics = Ballistics;
        var w = snap.Splashes;
        w.EnsureCapacity(splashCount);
        n = 0;
        long oldest = snap.Time.Ms - settings.SplashLifeMs;
        for (int k = 0; k < splashCount; k++)
        {
            ref readonly var sp = ref splashes[(splashNext - 1 - k + splashes.Length) % splashes.Length];
            if (sp.Ms < oldest)
                break;
            (w.Point[n], w.Ms[n], w.Ship[n], w.Shooter[n]) = (sp.Point, sp.Ms, sp.Ship, sp.Shooter);
            n++;
        }
        w.Count = n;
    }
}
