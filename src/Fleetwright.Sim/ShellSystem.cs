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
        /// <summary>The gunnery system's record of its salvo (for the spot), or -1 (the dev's fire); the shell type and
        /// the shooter's class (its registry id) it's of.</summary>
        public int Record, ShellType, ShooterClass;
        /// <summary>The shooter's dye when it was fired (her load, so it outlives her).</summary>
        public Dye Dye;
        public bool Alive;
        /// <summary>The ship it hits (an entity), or -1; and where it ends.</summary>
        public int Hit;
        public Vec3d End;
        /// <summary>A hit's point and the shell's direction in that ship's frame, and its speed, m/s.</summary>
        public P3 HitPoint, HitDirection;
        public double HitSpeed;
    }

    /// <summary>A splash, or a hit's mark (Ship: the ship hit, an entity), at a moment, and the shell's calibre; for the
    /// screens (docs/visuals/splashes.md), the angle it fell at (degrees under the horizontal), the bearing it was going
    /// (degrees from north), its kind and its dye.</summary>
    public readonly record struct Splash(Vec3d Point, long Ms, int Ship, int Salvo, int Shooter, double CalibreMm,
        float FallDeg = 90, float BearingDeg = 0, ShellKind Kind = ShellKind.Ap, Dye Dye = Dye.None);

    /// <summary>A mount fired, for the screens (docs/visuals/muzzle-flashes.md F0): when, who (an entity, and its class's
    /// registry id), which of the class's mounts, the salvo, how many of its barrels got a shell away, the mount's axis
    /// point and the way its first shell left (its heading over the sea, unit, and its elevation, degrees). Each mount
    /// has its own time, so a salvo's ripple (later) needs nothing new here.</summary>
    public readonly record struct Shot(long Ms, int Shooter, int Class, int Mount, int Salvo, int Barrels, Vec3d Muzzle, Vec3d Forward,
        float ElevationDeg);

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
    readonly Shot[] shots;
    int shotNext, shotCount;
    int nextSalvo;
    bool fired;
    double biggest;   // the biggest class's reach in the world (ShipSolids.Radius)
    int[] candidates = new int[16];
    readonly Vec3d[] chordPoint = new Vec3d[Chords + 2];
    readonly double[] chordTime = new double[Chords + 2];
    double[] bearings = new double[16];

    /// <summary>The world's ballistic tables (the renderer looks shells up in them, by the snapshot's table index).</summary>
    public BallisticsRegistry Ballistics { get; } = new();

    /// <summary>Shells in flight now; hits and misses so far.</summary>
    public int InFlight => alive;
    public int Hits { get; private set; }
    public int Misses { get; private set; }

    /// <summary>Told of each shell of a gunnery salvo as it comes down (set by the world).</summary>
    internal GunnerySystem? Gunnery { get; set; }

    /// <summary>Told of each hit as it lands (set by the world).</summary>
    internal DamageSystem? Damage { get; set; }

    internal ShellSystem(SimSettings settings, ShipSystem ships, ShipIndex index, CombatLog log)
    {
        (this.settings, this.ships, this.index, this.log) = (settings, ships, index, log);
        shells = new Shell[Math.Max(settings.ShellCapacity, 16)];
        free = new int[shells.Length];
        splashes = new Splash[Math.Max(settings.SplashCapacity, 16)];
        shots = new Shot[Math.Max(settings.ShotCapacity, 16)];
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
        if (bearings.Length < cls.Mounts.Length)
            bearings = new double[Math.Max(cls.Mounts.Length, bearings.Length * 2)];
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
            int away = 0;
            Vec3d first = default;
            double firstEl = 0;
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
                var dir = (v - up * vu).Normalized();
                double el = double.RadiansToDegrees(Math.Asin(vu / sp));
                if (Launch(world, table, muzzle, dir, el, sp / v0, salvo, ship.Entity, battery, -1, b.Fires, ship.ClassIndex) && away++ == 0)
                    (first, firstEl) = (dir, el);
            }
            if (away > 0)
                Fired(world, ship.Entity, ship.ClassIndex, m, salvo, away, muzzle, first, firstEl);
        }
    }

    /// <summary>A mount fired (<see cref="Shot"/>): into the ring the screens read. Its shells are launched apart.</summary>
    public void Fired(World world, int shooter, int classId, int mount, int salvo, int barrels, Vec3d muzzle, Vec3d forward, double elevationDeg)
    {
        shots[shotNext] = new Shot(world.Time.Ms, shooter, classId, mount, salvo, barrels, muzzle, forward, (float)elevationDeg);
        shotNext = (shotNext + 1) % shots.Length;
        shotCount = Math.Min(shotCount + 1, shots.Length);
    }

    /// <summary>A shell away: its record, its window and end solved, its window's event scheduled. False if it never
    /// comes down (no shell is made).</summary>
    public bool Launch(World world, int table, Vec3d muzzle, Vec3d forward, double elevationDeg, double speed, int salvo, int shooter, int battery,
        int record = -1, int shellType = -1, int shooterClass = -1)
    {
        var t = Ballistics[table];
        double end = t.Reaches(muzzle, forward, elevationDeg, speed, 0);
        if (double.IsNaN(end))
            return false;
        double top = t.Reaches(muzzle, forward, elevationDeg, speed, settings.ShellWindowTopM);
        if (double.IsNaN(top) || top > end || top < 0)
            top = 0;   // it never rises over the window's top (or tops out about it, where the solve can step back to
                       // before it was fired: a low gun at a few degrees, ShellSystemTests): all its flight is in the window
        int i = Allocate();
        ref var s = ref shells[i];
        int gen = s.Generation + 1;
        s = new Shell
        {
            Table = table, FiredMs = world.Time.Ms, Muzzle = muzzle, Forward = forward, ElevationDeg = elevationDeg, Speed = speed, WindowS = top,
            EndS = end, Salvo = salvo, Shooter = shooter, Battery = battery, Record = record, ShellType = shellType, ShooterClass = shooterClass,
            Generation = gen, Alive = true,
            Hit = -1,
            Dye = ships.Store.SlotOf(shooter) is int ss and >= 0 ? ships.Store[ss].Dye : Dye.None,
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
            var table = Ballistics[s.Table];
            table.Position(s.Muzzle, s.Forward, s.ElevationDeg, s.Speed, s.EndS, out var vel);
            var up = s.End.Normalized();
            double vu = Vec3d.Dot(vel, up), vl = Math.Max(vel.Length, 1e-9);
            float fall = (float)double.RadiansToDegrees(Math.Asin(Math.Clamp(-vu / vl, -1, 1)));
            float bearing = (float)Earth.Heading(s.End, vel - up * vu);
            var kind = s.ShooterClass >= 0 && s.ShellType >= 0 ? ships.Class(s.ShooterClass).ShellTypes[s.ShellType].Kind : ShellKind.Ap;
            splashes[splashNext] = new Splash(s.End, world.Time.Ms, s.Hit, s.Salvo, s.Shooter, table.Key.CalibreMm, fall, bearing, kind, s.Dye);
            splashNext = (splashNext + 1) % splashes.Length;
            splashCount = Math.Min(splashCount + 1, splashes.Length);
            if (s.Hit >= 0)
            {
                Hits++;
                if (Damage != null && settings.HitsDoDamage && s.ShooterClass >= 0 && s.ShellType >= 0)
                    Damage.Strike(world, new Hit
                    {
                        Ship = s.Hit, Ms = world.Time.Ms, Point = s.HitPoint, Direction = s.HitDirection, SpeedMs = s.HitSpeed, Source = HitSource.Shell,
                        Shell = ships.Class(s.ShooterClass).ShellTypes[s.ShellType], ShellType = s.ShellType, Shooter = s.Shooter, Battery = s.Battery,
                    });
                else
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
        // on under the water, for a shell that dives (a nose fuze fires on the water: a near miss)
        bool dives = s.ShooterClass < 0 || s.ShellType < 0 || ships.Class(s.ShooterClass).ShellTypes[s.ShellType].Fuze != Fuze.Nose;
        chordPoint[Chords + 1] = chordPoint[Chords] + vel * (dives ? settings.ShellUnderwaterM / speed : 0);
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
                    if (store[ship].Entity == s.Shooter)
                        continue;   // not its own ship (at short range the whole flight is in the window: it starts at the muzzle)
                    var cls = ships.Class(store[ship].ClassIndex);
                    long midMs = s.FiredMs + (long)Math.Round((chordTime[j] + chordTime[j + 1]) / 2 * 1000);
                    var frame = ships.FrameAt(ship, midMs);
                    var attitude = store.Combat.Condition(ship).Attitude;
                    int mounts = cls.Mounts.Length;
                    if (bearings.Length < mounts)
                        bearings = new double[Math.Max(mounts, bearings.Length * 2)];
                    for (int m = 0; m < mounts; m++)
                        bearings[m] = CombatStore.BearingAt(store.Combat.Mount(ship, m), cls.Mounts[m], midMs);
                    if (!HitQuery.Trace(cls, frame, attitude, chordPoint[j], chordPoint[j + 1], bearings.AsSpan(0, mounts), path))
                        continue;
                    foreach (ref readonly var x in path.Crossings)
                        if (x.Kind is CrossingKind.HullIn or CrossingKind.PartIn)
                        {
                            if (x.T < bestT)
                            {
                                bestT = x.T;
                                s.Hit = store[ship].Entity;
                                s.End = HitQuery.ToWorld(cls, frame, attitude, x.Point);
                                s.EndS = chordTime[j] + (chordTime[j + 1] - chordTime[j]) * x.T / Math.Max(path.Length, 1e-9);
                                double dx = path.End.X - path.Start.X, dy = path.End.Y - path.Start.Y, dz = path.End.Z - path.Start.Z;
                                double l = Math.Max(Math.Sqrt(dx * dx + dy * dy + dz * dz), 1e-9);
                                (s.HitPoint, s.HitDirection, s.HitSpeed) = (x.Point, new P3(dx / l, dy / l, dz / l), speed);
                            }
                            break;
                        }
                }
            }
        }
        world.Schedule(new WorldTime(Math.Max(world.Time.Ms, s.FiredMs + (long)Math.Floor(s.EndS * 1000))), EventTier.Fine, slot, Id, EndEvent, s.Generation);
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
            (w.Point[n], w.Ms[n], w.Ship[n], w.Shooter[n], w.CalibreMm[n]) = (sp.Point, sp.Ms, sp.Ship, sp.Shooter, sp.CalibreMm);
            (w.Salvo[n], w.FallDeg[n], w.BearingDeg[n], w.Kind[n], w.Dye[n]) = (sp.Salvo, sp.FallDeg, sp.BearingDeg, sp.Kind, sp.Dye);
            n++;
        }
        w.Count = n;

        var f = snap.Shots;
        f.EnsureCapacity(shotCount);
        n = 0;
        for (int k = 0; k < shotCount; k++)
        {
            ref readonly var sh = ref shots[(shotNext - 1 - k + shots.Length) % shots.Length];
            if (sh.Ms < oldest)
                break;
            (f.Ms[n], f.Shooter[n], f.Class[n], f.Mount[n], f.Salvo[n]) = (sh.Ms, sh.Shooter, sh.Class, sh.Mount, sh.Salvo);
            (f.Barrels[n], f.Muzzle[n], f.Forward[n], f.ElevationDeg[n]) = (sh.Barrels, sh.Muzzle, sh.Forward, sh.ElevationDeg);
            n++;
        }
        f.Count = n;
    }
}
