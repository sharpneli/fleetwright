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
/// to the water (and on under it as far as <see cref="WaterRun.ReachM"/>), in chords, goes through the hit query
/// against the ships the world's index finds near the fall, each where it is then; the first ship it meets is hit;</item>
/// <item>a hit it runs on under the water to has a third, when it meets the water: its splash there;</item>
/// <item>its end, the hit or the water: a splash (or a hit's mark) goes into the ring the screens read, and the shell's
/// record is freed.</item>
/// </list>
/// Shells don't depend on the clock's tier or tick: one fired lands where it lands. Records and splashes are pooled.
/// <para>The world registers it third, so its id is always <see cref="Id"/>.</para></summary>
public sealed class ShellSystem : IWorldSystem
{
    public const byte Id = 3;
    const ushort WindowEvent = 1, EndEvent = 2, WaterEvent = 3;
    const int Chords = 4;

    /// <summary>A shell in flight.</summary>
    public struct Shell
    {
        public int Table;
        public long FiredMs;
        public Vec3d Muzzle, Forward;
        public double ElevationDeg, Speed;
        /// <summary>Seconds after firing: the window's top, where it met the water (its end, unless it ran on under it to
        /// a hit), and the end (the hit, or the water until the window's run).</summary>
        public double WindowS, WaterS, EndS;
        /// <summary>Where it met the water (a hit under it splashes there too).</summary>
        public Vec3d WaterPoint;
        public int Salvo, Shooter, Battery, Generation;
        /// <summary>The gunnery system's record of its salvo (for the spot), or -1 (the dev's fire); and the shell type,
        /// of the shooter's loadout when it was fired (so it outlives her), null for none.</summary>
        public int Record, ShellType;
        public Loadout? Loadout;
        /// <summary>The shooter's dye when it was fired (her load, so it outlives her).</summary>
        public Dye Dye;
        public bool Alive;
        /// <summary>The ship it hits (an entity), or -1; and where it ends.</summary>
        public int Hit;
        public Vec3d End;
        /// <summary>A hit's point and the shell's direction in that ship's frame, its speed where it met the water (or
        /// the hull, in the air), m/s, and how far it ran under the water to the hull, m.</summary>
        public P3 HitPoint, HitDirection;
        public double HitSpeed, WaterRunM;
        /// <summary>How many times it has skipped off the water (E7's ricochet, <see cref="Skip"/>): a skipped shell flies on
        /// from where it skipped (its muzzle there, <see cref="FiredMs"/> then, <see cref="Speed"/> its speed off the water, m/s)
        /// on its own flight (<see cref="SkipFlight"/>), not its table's.</summary>
        public int Skips;
        /// <summary>The sea's face where it meets the water (<see cref="WaterSkip.Facet"/>: its slopes along and across its line,
        /// tangents), drawn as its window opens, and the angle it meets that face at, degrees (its fall on a flat sea); and
        /// its yaw off its last skip, degrees.</summary>
        public double FacetAlong, FacetAcross, MeetDeg, YawDeg;
    }

    /// <summary>What a shell does at the water as a miss: goes in (plunges, or bursts on it: a near miss), skips and flies
    /// on, or bursts in the air just past the graze (a base fuze it started: its near miss already struck).</summary>
    enum Graze : byte { In, Skipped, Burst }

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
    /// <summary>The near misses struck: a ship within a missed shell's burst's reach (E7; a tool's count, not state).</summary>
    public int NearMissCount { get; private set; }
    /// <summary>The skips off the water, and the shells that skipped at least once (E7's ricochet; a tool's count, not state).</summary>
    public int SkipCount { get; private set; }
    public int SkippedShells { get; private set; }
    /// <summary>Misses that could have skipped but met the sea's face too steeply and went in; and the grazes a fuze burst on,
    /// a nose fuze's or a base fuze's the graze started (a tool's counts, not state).</summary>
    public int PlungedCount { get; private set; }
    public int GrazeBursts { get; private set; }

    /// <summary>Told of each shell of a gunnery salvo as it comes down (set by the world).</summary>
    internal GunnerySystem? Gunnery { get; set; }

    /// <summary>Told of each hit as it lands (set by the world).</summary>
    internal DamageSystem? Damage { get; set; }

    internal ShellSystem(SimSettings settings, ShipSystem ships, ShipIndex index, CombatLog log)
    {
        (this.settings, this.ships, this.index, this.log) = (settings, ships, index, log);
        shells = new Shell[Math.Max(settings.ShellCapacity, 16)];
        free = new int[shells.Length];
        (flights, flightN) = (new double[shells.Length * SkipFlight.Length], new int[shells.Length]);
        splashes = new Splash[Math.Max(settings.SplashCapacity, 16)];
        shots = new Shot[Math.Max(settings.ShotCapacity, 16)];
    }

    /// <summary>A shell's record by its slot (the snapshot's), for tests and tools.</summary>
    public ref readonly Shell this[int slot] => ref shells[slot];
    public int Slots => high;

    // ------------------------------------------------------------------ commands

    /// <summary>The dev's "fire at": a ship's battery fires a salvo at a point on the sea past fire control, each mount
    /// laid on it at once (wherever it pointed, inside its traverse), each gun's elevation from its shell's range table,
    /// each shell with its guns' own dispersion (<see cref="FireControl.Dispersion(BallisticTable, double, double, double, double, SimSettings, double, double)"/>,
    /// the battery's pattern by the flight, scaled by <see cref="GunneryErrorScale.Dispersion"/>), drawn from the stream
    /// "fire" by salvo and shell. Each mount fires its next load in the magazine order, or with a kind its first load of
    /// that kind; a mount with no rounds of it fires nothing. The rounds are spent (no reload).</summary>
    public static Command FireAt(int shooter, int battery, double latDeg, double lonDeg, ShellKind? kind = null) =>
        new(Id, (ushort)ShellCommand.FireAt, shooter, A: Earth.PackLatLon(latDeg, lonDeg), B: battery, X: kind is { } k ? (int)k + 1 : 0);

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
        FireBattery(world, slot, cls, c.B, Earth.Position(lat, lon), c.X >= 1 ? (ShellKind)(int)(c.X - 1) : null);
    }

    /// <summary>The tables for a class's batteries, and the hit query's room for its solids, made the first time it's in
    /// the world (not at its first salvo or near miss).</summary>
    internal void Prepare(ShipClass cls)
    {
        biggest = Math.Max(biggest, cls.Solids.Radius);
        if (bearings.Length < cls.Mounts.Length)
            bearings = new double[Math.Max(cls.Mounts.Length, bearings.Length * 2)];
        path.Reserve(cls.Solids.Solids.Length);   // the hit query's room for it, now rather than at its first near miss
        Prepare(cls.Loadout);
    }

    /// <summary>The tables for every load of a loadout's batteries (a class's own, or one a ship is given), made when it
    /// comes into the world.</summary>
    internal void Prepare(Loadout lo)
    {
        for (int b = 0; b < lo.Class.Batteries.Length; b++)
            foreach (var l in lo.Loads(b))
                Ballistics.Ensure(KeyOf(lo, b, l.ShellType));
    }

    // ------------------------------------------------------------------ skipping off the water (E7's ricochet)

    /// <summary>Each slot's skip flight (<see cref="SkipFlight"/>: <see cref="SkipFlight.Length"/> doubles a slot) and its
    /// samples, sized with the records: a skipped shell flies on these, not its table.</summary>
    double[] flights;
    int[] flightN;

    /// <summary>Where a shell is t seconds after it was fired (or skipped), Earth-centred, and its velocity: its table's flight,
    /// or its skip's.</summary>
    Vec3d Fly(in Shell s, int slot, double t, out Vec3d velocity) =>
        s.Skips > 0
            ? SkipFlight.Position(s.Muzzle, s.Forward, flights.AsSpan(slot * SkipFlight.Length, SkipFlight.Length), flightN[slot], t, out velocity)
            : Ballistics[s.Table].Position(s.Muzzle, s.Forward, s.ElevationDeg, s.Speed, t, out velocity);

    /// <summary>Whether a shell can still skip: its skips not used up, its shell known.</summary>
    bool CanSkip(in Shell s) => s.Skips < settings.WaterRicochetSkips && s.Loadout != null && s.ShellType >= 0;

    /// <summary>A skip's draws (the stream "skips", by salvo, shell and skip): part 0 the face it meets, 1 what leaves it.</summary>
    static RandomStream SkipDraws(World world, in Shell s, int slot, int part) =>
        world.Stream("skips").Derive(((ulong)(uint)s.Salvo << 32) | (uint)slot).Derive((ulong)(2 * s.Skips + part));

    /// <summary>The face of the sea a shell meets the water on (<see cref="WaterSkip.Facet"/>), at a point with its velocity
    /// there falling at fallDeg: the world's sea's slopes along and across its way (<see cref="RollSea.SlopeRms"/>, times
    /// <see cref="SimSettings.WaterRicochetSeaK"/>), and the angle it meets that face at. A flat sea: its fall.</summary>
    void Facet(World world, ref Shell s, int slot, Vec3d at, Vec3d vel, double fallDeg)
    {
        (s.FacetAlong, s.FacetAcross, s.MeetDeg) = (0, 0, fallDeg);
        double k = settings.WaterRicochetSeaK, v = vel.Length;
        if (!(k > 0) || s.Loadout == null || !(v > 1))
            return;
        var up = at.Normalized();
        var level = vel - up * Vec3d.Dot(vel, up);
        if (!(level.Length > 1e-9))
            return;
        var (ra, rc) = world.Sea.SlopeRms(Earth.Heading(at, level));
        if (!(ra > 0) && !(rc > 0))
            return;
        var r = SkipDraws(world, s, slot, 0);
        var (along, across) = WaterSkip.Facet(fallDeg, k * ra, k * rc, ref r);
        var forward = level.Normalized();
        var n = WaterSkip.Normal(up, forward, Vec3d.Cross(forward, up), along, across);
        double meet = double.RadiansToDegrees(Math.Asin(Math.Clamp(-Vec3d.Dot(vel, n) / v, -1, 1)));
        (s.FacetAlong, s.FacetAcross, s.MeetDeg) = (along, across, meet);
    }

    /// <summary>A shell's near-miss draws (the stream "near misses", by salvo and shell): its fuze on the water, its filler.</summary>
    static RandomStream NearDraws(World world, in Shell s, int slot) => world.Stream("near misses").Derive(((ulong)(uint)s.Salvo << 32) | (uint)slot);

    /// <summary>A shell that missed meets the water (docs/battle/shells-armour-damage.md E7's ricochet; <see cref="WaterSkip"/>,
    /// research damage-research/16-water-ricochet.md): meeting the sea's face (<see cref="Shell.MeetDeg"/>) at
    /// <see cref="SimSettings.WaterRicochetDeg"/> or steeper it goes in. Flatter, a nose fuze the sea meets hard enough fires
    /// on the graze (<see cref="WaterSkip.NoseFires"/>, then its near miss's draws: <see cref="NearMiss.Burst"/>), and it goes
    /// in as a near miss; else it skips: <see cref="WaterSkip.Kept"/> of its speed, off the face at <see cref="WaterSkip.ExitDeg"/>
    /// over it, turned <see cref="WaterSkip.AcrossDeg"/> about its normal, yawed (the stream "skips", by salvo, shell and
    /// skip). A base fuze the graze started bursts its delay on, in the air (<see cref="WaterSkip.BaseOnGraze"/>: its near
    /// miss struck here); the rest fly on from there (<see cref="SkipFlight"/>, its yaw's drag dying away): its window, its
    /// hits, its end and its skips as any shell's.</summary>
    Graze Skip(World world, ref Shell s, int slot)
    {
        if (!CanSkip(s))
            return Graze.In;
        Fly(s, slot, s.WaterS, out var vel);
        double v = vel.Length, meet = s.MeetDeg;
        if (!(v > 1) || !(meet < settings.WaterRicochetDeg))
        {
            PlungedCount++;
            return Graze.In;
        }
        var shell = s.Loadout!.ShellTypes[s.ShellType];
        if (shell.Fuze == Fuze.Nose && shell.FillerKg > 0 && WaterSkip.NoseFires(v, meet, settings))
        {
            var rng = NearDraws(world, s, slot);
            if (NearMiss.Burst(shell, v, meet, settings, ref rng).Force > 0)
            {
                GrazeBursts++;
                return Graze.In;   // it bursts on the graze: a near miss's (the same draws)
            }
        }
        var r = SkipDraws(world, s, slot, 1);
        double kept = WaterSkip.Kept(meet, settings), v1 = v * kept;
        double exit = WaterSkip.ExitDeg(meet, settings, ref r);
        double turn = double.DegreesToRadians(WaterSkip.AcrossDeg(kept, DesignerPlaceholders.RiflingHand, settings, ref r));
        double yaw = WaterSkip.YawDeg(meet, exit, settings, ref r);
        // off the face: along it in the shell's way, raised by the exit angle over it, turned right about its normal
        var up = s.WaterPoint.Normalized();
        var forward = (vel - up * Vec3d.Dot(vel, up)).Normalized();
        var n = WaterSkip.Normal(up, forward, Vec3d.Cross(forward, up), s.FacetAlong, s.FacetAcross);
        var along = (vel - n * Vec3d.Dot(vel, n)).Normalized();
        double e = double.DegreesToRadians(exit);
        var line = along * Math.Cos(e) + n * Math.Sin(e);
        line = line * Math.Cos(turn) - Vec3d.Cross(n, line) * Math.Sin(turn) + n * (Vec3d.Dot(n, line) * (1 - Math.Cos(turn)));
        double lu = Vec3d.Dot(line, up), rise = Math.Max(double.RadiansToDegrees(Math.Asin(Math.Clamp(lu, -1, 1))), MinExitDeg);
        var dir = (line - up * lu).Normalized();
        var (delay, force) = WaterSkip.BaseOnGraze(shell, v, settings, ref r);
        if (force > 0)
        {
            // started by the graze: it bursts its delay on, over the water
            double rr = double.DegreesToRadians(rise);
            var air = dir * Math.Cos(rr) + up * Math.Sin(rr);
            StrikeNear(world, s, shell, s.WaterPoint + air * (v1 * delay), air, v1, 0, force);
            GrazeBursts++;
            return Graze.Burst;
        }
        var f = flights.AsSpan(slot * SkipFlight.Length, SkipFlight.Length);
        int nf = SkipFlight.Fly(Combat.Ballistics.DragK(Ballistics[s.Table].Key), rise, v1, f, yaw,
            settings.WaterRicochetYawCalibres * shell.CalibreMm / 1000, settings.WaterRicochetYawDragK);
        double end = SkipFlight.Descends(f, nf, 0);
        if (double.IsNaN(end) || !(end > 0))
            return Graze.In;
        double top = SkipFlight.Descends(f, nf, settings.ShellWindowTopM);
        if (double.IsNaN(top) || top > end || top < 0)
            top = 0;   // it never rises over the window's top: all its flight is in the window
        flightN[slot] = nf;
        (s.FiredMs, s.Muzzle, s.Forward, s.ElevationDeg, s.Speed, s.YawDeg) = (world.Time.Ms, s.WaterPoint, dir, rise, v1, yaw);
        (s.WindowS, s.WaterS, s.EndS, s.Hit, s.Skips, s.Generation) = (top, end, end, -1, s.Skips + 1, s.Generation + 1);
        SkipCount++;
        SkippedShells += s.Skips == 1 ? 1 : 0;
        world.Schedule(new WorldTime(s.FiredMs + (long)Math.Floor(top * 1000)), EventTier.Fine, slot, Id, WindowEvent, s.Generation);
        return Graze.Skipped;
    }

    /// <summary>A shell's yaw where it ends, degrees: its last skip's, died away e-fold every
    /// <see cref="SimSettings.WaterRicochetYawCalibres"/> along its way since (0: it never skipped).</summary>
    double YawAt(in Shell s, int slot)
    {
        if (s.Skips == 0 || !(s.YawDeg > 0) || s.Loadout == null || s.ShellType < 0)
            return 0;
        var (arc, _, _, _) = SkipFlight.At(flights.AsSpan(slot * SkipFlight.Length, SkipFlight.Length), flightN[slot], Math.Min(s.EndS, s.WaterS));
        double decay = settings.WaterRicochetYawCalibres * s.Loadout.ShellTypes[s.ShellType].CalibreMm / 1000;
        return s.YawDeg * Math.Exp(-Math.Max(arc, 0) / Math.Max(decay, 1e-3));
    }

    /// <summary>The flattest a skip leaves at, degrees (a scatter's draw under it: as flat as a shell can rise off the sea).</summary>
    const double MinExitDeg = 0.2;

    static BallisticKey KeyOf(Loadout lo, int battery, int shellType) =>
        BallisticKey.Of(lo.ShellTypes[shellType], lo.Class.Mounts[lo.Class.Links(lo.Class.Batteries[battery].Mounts)[0]].MaxElevationDeg);

    /// <summary>The index of a battery's table in <see cref="Ballistics"/> for a loadout's lead shell (built now if it's
    /// new).</summary>
    public int TableOf(Loadout lo, int battery) => TableOf(lo, battery, lo.Fires(battery));

    /// <summary>The same for one of its shells.</summary>
    public int TableOf(Loadout lo, int battery, int shellType) => Ballistics.Ensure(KeyOf(lo, battery, shellType));

    /// <summary>A new salvo's number.</summary>
    public int NewSalvo() => nextSalvo++;

    void FireBattery(World world, int slot, ShipClass cls, int battery, Vec3d target, ShellKind? kind)
    {
        if (kind == ShellKind.Star)
            return;   // star shell bursts in the air by its time fuze: the star shell system's (docs/battle/night.md Q5)
        var ship = ships.Store[slot];
        ref readonly var b = ref cls.Batteries[battery];
        var frame = ship.Frame;
        var shipVelocity = ship.Forward * ship.Speed;
        double wl = cls.Vertical.Waterline;
        int salvo = nextSalvo++;
        var stream = world.Stream("fire").Derive((ulong)salvo);
        int k = 0;
        var combat = ships.Store.Combat;
        var lo = combat.Loadout(slot);
        foreach (int m in cls.Links(b.Mounts))
        {
            int load = kind is { } want ? combat.LoadOf(slot, m, want) : combat.FiresNext(slot, m);
            if (load < 0)
            {
                k += cls.TurretTypes[cls.Mounts[m].Type].Barrels;   // the same stream for the guns that do fire
                continue;
            }
            int type = lo.Loads(battery)[load].ShellType, table = TableOf(lo, battery, type);
            var t = Ballistics[table];
            double v0 = t.Key.MuzzleVelocityMs;
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
            // the guns' own dispersion, as the gunnery's salvos have it about their centre (the battery's pattern by the
            // flight, ES7), and nothing of fire control's; the mount laid on the line at once, wherever it pointed
            double e0 = t.ElevationFor(range, h0);
            var (sr, sd) = FireControl.Dispersion(t, b.PatternPct, range, e0, h0, settings);
            double ks = settings.GunneryErrors.Dispersion;
            combat.LayNow(slot, m, mount, world.Time.Ms, ((Earth.Bearing(ship.Position, target) - ship.HeadingDeg) % 360 + 360) % 360,
                double.IsNaN(e0) ? mount.MaxElevationDeg : e0);
            for (int g = 0; g < cls.TurretTypes[mount.Type].Barrels; g++, k++)
            {
                var rng = stream.Derive((ulong)k);
                double aim = range + rng.NextGaussian() * ks * sr, across = rng.NextGaussian() * ks * sd / Math.Max(range, 1);
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
                if (Launch(world, table, muzzle, dir, el, sp / v0, salvo, ship.Entity, battery, -1, type, lo) && away++ == 0)
                    (first, firstEl) = (dir, el);
            }
            combat.Spend(slot, m, load, cls.TurretTypes[mount.Type].Barrels);
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
        int record = -1, int shellType = -1, Loadout? loadout = null)
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
            WaterS = end, EndS = end, Salvo = salvo, Shooter = shooter, Battery = battery, Record = record, ShellType = shellType, Loadout = loadout,
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
            Array.Resize(ref flights, shells.Length * SkipFlight.Length);
            Array.Resize(ref flightN, shells.Length);
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
        else if (e.Code == WaterEvent)
            AddSplash(SplashOf(ref s, e.Entity, s.WaterPoint, world.Time.Ms, -1));   // a hit under the water: its splash, as it met it
        else if (e.Code == EndEvent)
        {
            var splash = SplashOf(ref s, e.Entity, s.End, world.Time.Ms, s.Hit);
            AddSplash(splash);
            if (s.Hit >= 0)
            {
                Hits++;
                if (Damage != null && settings.HitsDoDamage && s.Loadout != null && s.ShellType >= 0)
                    Damage.Strike(world, new Hit
                    {
                        Ship = s.Hit, Ms = world.Time.Ms, Point = s.HitPoint, Direction = s.HitDirection, SpeedMs = s.HitSpeed, WaterRunM = s.WaterRunM,
                        YawDeg = YawAt(s, e.Entity),
                        Source = HitSource.Shell,
                        Shell = s.Loadout.ShellTypes[s.ShellType], ShellType = s.ShellType, Shooter = s.Shooter, Battery = s.Battery,
                    });
                else
                    log.Add(new CombatEntry(world.Time.Ms, CombatEvent.Hit, s.Shooter, s.Battery, s.Hit, s.ShellType));
            }
            else
            {
                var graze = Skip(world, ref s, e.Entity);
                if (graze == Graze.Skipped)
                {
                    // off the water and on (E7's ricochet): its splash is where it skipped, and so the spotter's
                    if (s.Record >= 0)
                        Gunnery?.Landed(world, s.Record, splash.Point, -1, splash.FallDeg);
                    s.Record = -1;
                    return;
                }
                Misses++;
                if (graze == Graze.In)
                    NearMisses(world, ref s, e.Entity);
            }
            s.Alive = false;
            alive--;
            free[freeCount++] = e.Entity;
            if (s.Record >= 0)
                Gunnery?.Landed(world, s.Record, s.End, s.Hit, splash.FallDeg);
        }
    }

    /// <summary>A shell that missed bursts in the water (docs/battle/shells-armour-damage.md E7): when a ship's waterplane
    /// lies within its reach (<see cref="NearMiss.ReachM"/>, from where its run under the water could end), its fuze and
    /// filler are drawn (the stream "near misses", by salvo and shell), and each ship within the reach of the burst takes
    /// a near miss there. Most misses cost the one look at the index.</summary>
    void NearMisses(World world, ref Shell s, int slot)
    {
        if (Damage == null || !settings.HitsDoDamage || s.Loadout == null || s.ShellType < 0)
            return;
        var shell = s.Loadout.ShellTypes[s.ShellType];
        double reach = NearMiss.ReachM(shell, settings);
        if (!(reach > 0))
            return;
        Fly(s, slot, s.WaterS, out var vel);
        double v = Math.Max(vel.Length, 1e-9), decay = WaterRun.DecayM(shell, settings);
        double fall = s.MeetDeg;   // the face it met (its fall on a flat sea)
        // as far as its fuze could carry it under the water (a base fuze's delay at four of its scatters)
        double longest = WaterRun.RunInM(v, shell.FuzeWaterMs / 1000 + (shell.Fuze == Fuze.Base ? shell.FuzeDelayS * (1 + 4 * shell.FuzeScatter) : 0), decay);
        if (index.Near(s.WaterPoint, reach + longest + biggest).Length == 0)
            return;
        var rng = NearDraws(world, s, slot);
        var (run, force) = NearMiss.Burst(shell, v, fall, settings, ref rng);
        if (!(force > 0))
            return;
        var dir = vel * (1 / v);
        StrikeNear(world, s, shell, s.WaterPoint + dir * run, dir, WaterRun.Speed(v, run, decay), run, force);
    }

    /// <summary>A missed shell's burst at a point (in the water, or just over it), flying along dir at speed: each ship whose
    /// waterplane lies within its reach takes a near miss (its run under the water before it, m).</summary>
    void StrikeNear(World world, in Shell s, in ShellTypeRow shell, Vec3d at, Vec3d dir, double speed, double run, double force)
    {
        double reach = NearMiss.ReachM(shell, settings);
        var near = index.Near(at, reach + biggest);
        if (Damage == null || !settings.HitsDoDamage || near.Length == 0)
            return;
        if (candidates.Length < near.Length)
            candidates = new int[Math.Max(near.Length, candidates.Length * 2)];
        near.CopyTo(candidates);
        int n = near.Length;
        var store = ships.Store;
        long now = world.Time.Ms;
        for (int c = 0; c < n; c++)
        {
            int ship = candidates[c];
            int entity = store[ship].Entity;
            if (entity == s.Shooter)
                continue;
            var cls = ships.Class(store[ship].ClassIndex);
            var frame = ships.FrameAt(ship, now);
            var attitude = store.Combat.Condition(ship).AttitudeAt(now);
            var p = HitQuery.ToShip(cls, frame, attitude, at);
            if (NearMiss.WaterplaneM(cls, p.X, p.Y) > reach)
                continue;
            var q = HitQuery.ToShip(cls, frame, attitude, at + dir);
            NearMissCount++;
            Damage.Strike(world, new Hit
            {
                Ship = entity, Ms = now, Point = p, Direction = new P3(q.X - p.X, q.Y - p.Y, q.Z - p.Z), SpeedMs = speed, WaterRunM = run,
                Source = HitSource.NearMiss, Shell = shell, ShellType = s.ShellType, Shooter = s.Shooter, Battery = s.Battery, Force = force,
            });
        }
    }

    /// <summary>A shell's splash (or a hit's mark) at a point and moment: the angle it fell at and the way it flew where it
    /// came down to the water (or the hull, in the air).</summary>
    Splash SplashOf(ref Shell s, int slot, Vec3d at, long ms, int ship)
    {
        var table = Ballistics[s.Table];
        Fly(s, slot, Math.Min(s.EndS, s.WaterS), out var vel);
        var up = at.Normalized();
        double vu = Vec3d.Dot(vel, up), vl = Math.Max(vel.Length, 1e-9);
        float fall = (float)double.RadiansToDegrees(Math.Asin(Math.Clamp(-vu / vl, -1, 1)));
        float bearing = (float)Earth.Heading(at, vel - up * vu);
        var kind = s.Loadout != null && s.ShellType >= 0 ? s.Loadout.ShellTypes[s.ShellType].Kind : ShellKind.Ap;
        return new Splash(at, ms, ship, s.Salvo, s.Shooter, table.Key.CalibreMm, fall, bearing, kind, s.Dye);
    }

    void AddSplash(in Splash sp)
    {
        splashes[splashNext] = sp;
        splashNext = (splashNext + 1) % splashes.Length;
        splashCount = Math.Min(splashCount + 1, splashes.Length);
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
            chordPoint[j] = Fly(s, slot, chordTime[j], out vel);
        }
        double speed = vel.Length;
        // on under the water, straight along its flight, as far as it gets (a nose fuze fires on the water: a near miss)
        var shell = s.Loadout != null && s.ShellType >= 0 ? s.Loadout.ShellTypes[s.ShellType] : new ShellTypeRow { CalibreMm = t.Key.CalibreMm };
        // a shell that will skip off the water (E7's ricochet) doesn't run on under it
        var upEnd = chordPoint[Chords].Normalized();
        double fallEnd = double.RadiansToDegrees(Math.Asin(Math.Clamp(-Vec3d.Dot(vel, upEnd) / Math.Max(speed, 1e-9), -1, 1)));
        Facet(world, ref s, slot, chordPoint[Chords], vel, fallEnd);
        double reach = CanSkip(s) && s.MeetDeg < settings.WaterRicochetDeg ? 0 : WaterRun.ReachM(shell, speed, settings);
        chordPoint[Chords + 1] = chordPoint[Chords] + vel * (reach / speed);
        chordTime[Chords + 1] = s.EndS + WaterRun.TimeS(speed, reach, WaterRun.DecayM(shell, settings));
        (s.End, s.Hit, s.WaterRunM, s.WaterS, s.WaterPoint) = (chordPoint[Chords], -1, 0, s.EndS, chordPoint[Chords]);

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
                    var k = store.Combat.Condition(ship);
                    int mounts = cls.Mounts.Length;
                    if (bearings.Length < mounts)
                        bearings = new double[Math.Max(mounts, bearings.Length * 2)];
                    for (int m = 0; m < mounts; m++)
                        bearings[m] = CombatStore.BearingAt(store.Combat.Mount(ship, m), cls.Mounts[m], midMs);
                    // a ship going down sinks under it; one broken in two is two pieces, each traced on its own attitude for what's on
                    // its side of the break; an end that's come off stops nothing (docs/visuals/destruction.md decision 1)
                    bool broken = k.Broken;
                    for (int piece = 0; piece < (broken ? 2 : 1); piece++)
                    {
                        var attitude = broken ? k.PieceAt(midMs, bow: piece == 0) : k.AttitudeAt(midMs);
                        double xLo = broken && piece == 0 ? k.BreakX : k.CutSternX, xHi = broken && piece == 1 ? k.BreakX : k.CutBowX;
                        if (!HitQuery.Trace(cls, frame, attitude, chordPoint[j], chordPoint[j + 1], bearings.AsSpan(0, mounts), path))
                            continue;
                        var xs = path.Crossings;
                        for (int i = 0; i < xs.Length; i++)
                        {
                            ref readonly var x = ref xs[i];
                            if (x.Kind is CrossingKind.HullIn or CrossingKind.PartIn && x.Point.X >= xLo && x.Point.X <= xHi)
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
                                    s.WaterRunM = WaterRun.RunM(path, i);   // the ship's own waterplane, at its flotation
                                }
                                break;
                            }
                        }
                    }
                }
            }
        }
        // a hit under the water: its splash when it met the water, then the hit when it gets to the hull
        if (s.Hit >= 0 && s.WaterRunM > 0)
            world.Schedule(new WorldTime(Math.Max(world.Time.Ms, s.FiredMs + (long)Math.Floor(s.WaterS * 1000))), EventTier.Fine, slot, Id, WaterEvent, s.Generation);
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
                h.Add(s.Skips);
            }
        h.Add(Hits);
        h.Add(Misses);
    }

    public void Fill(Snapshot snap)
    {
        var v = snap.Shells;
        v.EnsureCapacity(alive);
        int n = 0, packed = 0;
        for (int i = 0; i < high; i++)
            if (shells[i].Alive)
            {
                ref readonly var s = ref shells[i];
                (v.Table[n], v.FiredMs[n], v.Muzzle[n], v.Forward[n], v.ElevationDeg[n], v.Speed[n], v.EndS[n], v.Hit[n], v.Shooter[n]) =
                    (s.Table, s.FiredMs, s.Muzzle, s.Forward, s.ElevationDeg, s.Speed, s.EndS, s.Hit, s.Shooter);
                (v.WaterS[n], v.Water[n], v.End[n], v.Skips[n]) = (s.WaterS, s.WaterPoint, s.End, s.Skips);
                if (s.Skips > 0)
                {
                    int len = flightN[i] * SkipFlight.Fields;
                    v.EnsureFlights(packed + len);
                    flights.AsSpan(i * SkipFlight.Length, len).CopyTo(v.Flights.AsSpan(packed));
                    (v.FlightAt[n], v.FlightN[n], packed) = (packed, flightN[i], packed + len);
                }
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
