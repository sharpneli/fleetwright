using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim;

/// <summary>What the torpedo system understands (<see cref="TorpedoSystem"/>).</summary>
public enum TorpedoCommand : ushort
{
    Launch = 1,
}

/// <summary>How a torpedo's run ended: it hit a ship, ran to its range and sank, or failed and sank on the way.</summary>
public enum TorpedoEnd : byte
{
    Hit,
    RunOut,
    Failed,
}

/// <summary>Torpedoes (docs/battle/fighting.md 4.6). A launch is ordered at a ship its navy holds in sight: each working,
/// loaded launcher that bears (a trained mount within its arcs, a fixed one within its gyro angle of its own bearing)
/// takes a share of the spread; a mount trains to the course first. At the launch the aim is solved again from the navy's
/// estimate of the target (<see cref="TorpedoAim"/>: the fast setting if it reaches, else the slow), the depth set for the
/// target as it's taken, the spread fanned over its length at the meeting point, each torpedo with its own course error;
/// some fail and sink on the way.
/// <para>A torpedo is a few numbers: where and when it entered the water, its heading, speed and depth; where it is is
/// closed form on its great circle. It costs one event a <see cref="SimSettings.TorpedoCheckMs"/>: the next piece of its
/// run is swept against the ships the world's index finds near it, each in its frame at the piece's two ends (at the set
/// depth, through the one tracer), so a hit doesn't depend on the tick; the first hull or part met is hit, at its instant,
/// and the hit goes to the damage system (<see cref="Underwater"/>). Not in its first <see cref="SimSettings.TorpedoArmM"/>
/// (arming). At the end of its run it sinks. Torpedoes don't depend on the clock's tier: one fired runs where it runs.
/// Records are pooled. World state: replays, hashes. The world registers it sixth, so its id is always
/// <see cref="Id"/>.</para></summary>
public sealed class TorpedoSystem : IWorldSystem
{
    public const byte Id = 6;
    const ushort LaunchEvent = 1, CheckEvent = 2, HitEvent = 3, EndEvent = 4;
    const double NearMarginM = 100;   // how far a ship near the piece can go in it

    /// <summary>A torpedo running.</summary>
    public struct Torpedo
    {
        public bool Alive;
        public int Generation;
        public long LaunchMs;
        /// <summary>Where it entered the water (on the sea's surface) and its heading there.</summary>
        public Vec3d Origin, Forward;
        /// <summary>Its speed, m/s; the depth it runs at, m; seconds after launch it ends (its run's end, where it fails,
        /// or the hit).</summary>
        public double SpeedMs, DepthM, EndS;
        public int Shooter, Launcher, ShooterClass, Row;
        /// <summary>Its number among all fired (the snapshot's, so the screens tell the torpedoes of a spread apart), and
        /// whether it fails on the way (its end short of its range).</summary>
        public long Serial;
        public bool Failed;
        /// <summary>The ship it hits (an entity), or -1; the hit's point and the torpedo's way through the water in that
        /// ship's frame, and its speed against it.</summary>
        public int Hit;
        public P3 HitPoint, HitDirection;
        public double HitSpeed;
    }

    /// <summary>A launch ordered, waiting for its mount to train: whose launcher, at whom, which of the spread.</summary>
    struct Pending
    {
        public bool Alive;
        public int Generation, Shooter, Launcher, Target, First, Count, Spread;
        public bool Truth;
    }

    readonly SimSettings settings;
    readonly ShipSystem ships;
    readonly ShipIndex index;
    readonly CombatLog log;
    readonly RandomStream stream;
    readonly HitPath path = new();
    Torpedo[] torpedoes;
    int[] free;
    int freeCount, high, alive;
    Pending[] pending = new Pending[16];
    readonly Stack<int> freePending = new(16);
    int highPending;
    /// <summary>A torpedo's end as the screens read it: where (on the surface over it), when, the ship hit (or -1), who
    /// fired it, its calibre and charge, its serial and how it ended.</summary>
    readonly record struct Ending(Vec3d Point, long Ms, int Ship, int Shooter, double CalibreMm, double ChargeKg, long Serial, TorpedoEnd How);

    readonly Ending[] ends;
    int endNext, endCount;
    long serial;
    bool fired;
    double biggest;
    int[] candidates = new int[16];
    double[] bearings = new double[16];
    readonly int[] launched = new int[64];

    /// <summary>Torpedoes running now; hits and ends (sunk at the end of their run, or failed) so far.</summary>
    public int Running => alive;
    public int Hits { get; private set; }
    public int Ended { get; private set; }
    public long Fired => serial;

    /// <summary>Told of each hit as it strikes (set by the world).</summary>
    internal DamageSystem? Damage { get; set; }

    internal TorpedoSystem(SimSettings settings, ShipSystem ships, ShipIndex index, CombatLog log, ulong seed)
    {
        (this.settings, this.ships, this.index, this.log) = (settings, ships, index, log);
        stream = RandomStream.Named(seed, "torpedoes");
        torpedoes = new Torpedo[Math.Max(settings.TorpedoCapacity, 16)];
        free = new int[torpedoes.Length];
        ends = new Ending[Math.Max(settings.TorpedoEndCapacity, 16)];
    }

    /// <summary>A torpedo's record by its slot (the snapshot's), for tests and tools.</summary>
    public ref readonly Torpedo this[int slot] => ref torpedoes[slot];
    public int Slots => high;

    /// <summary>The tracer's room for a class's solids, made the first time it's in the world.</summary>
    internal void Prepare(ShipClass cls)
    {
        biggest = Math.Max(biggest, cls.Solids.Radius);
        if (bearings.Length < cls.Mounts.Length)
            bearings = new double[Math.Max(cls.Mounts.Length, bearings.Length * 2)];
        path.Reserve(cls.Solids.Solids.Length);
    }

    // ------------------------------------------------------------------ commands

    /// <summary>A ship fires a spread of n torpedoes at a ship (an entity) its navy holds in sight, from the launchers that
    /// bear; the dev's (<paramref name="truth"/>) aimed at the ship as it truly is, in sight or not.</summary>
    public static Command Launch(int shooter, int target, int n, bool truth = false) =>
        new(Id, (ushort)TorpedoCommand.Launch, shooter, A: target, B: n, X: truth ? 1 : 0);

    public void Apply(World world, in Command c)
    {
        if ((TorpedoCommand)c.Code == TorpedoCommand.Launch)
            Order(world, c.Entity, (int)c.A, c.B, c.X != 0);
    }

    /// <summary>How many torpedoes a ship could fire at a target now: in its working, loaded launchers that bear on the
    /// course that meets it (none when no setting reaches it or its navy hasn't it in sight). The captains ask it.</summary>
    public int Ready(World world, int shooter, int target)
    {
        var store = ships.Store;
        int slot = store.SlotOf(shooter);
        if (slot < 0)
            return 0;
        var ship = store[slot];
        var cls = ship.Handling.Class;
        var k = store.Combat.Condition(slot);
        if (k.Status != ShipStatus.Afloat)
            return 0;
        long now = world.Time.Ms;
        int n = 0;
        for (int l = 0; l < cls.Launchers.Length; l++)
        {
            ref readonly var row = ref cls.Launchers[l];
            if (!k.LauncherWorks(l) || Busy(shooter, l))
                continue;
            int loaded = store.Combat.Launcher(slot, l, row, now).Loaded;
            if (loaded > 0 && Solve(world, ship, row, target, now, false, out _, out _, out double heading, out _) &&
                Bears(cls, row, Wrap(heading - ship.HeadingDeg)))
                n += loaded;
        }
        return n;
    }

    void Order(World world, int shooter, int target, int n, bool truth)
    {
        var store = ships.Store;
        int slot = store.SlotOf(shooter);
        if (slot < 0 || target == shooter || n <= 0)
            return;
        var ship = store[slot];
        var cls = ship.Handling.Class;
        var k = store.Combat.Condition(slot);
        if (k.Status != ShipStatus.Afloat)
            return;
        long now = world.Time.Ms;
        int first = 0, made = 0;
        for (int l = 0; l < cls.Launchers.Length && first < n && made < launched.Length; l++)
        {
            ref readonly var row = ref cls.Launchers[l];
            if (!k.LauncherWorks(l) || Busy(shooter, l))
                continue;
            int loaded = store.Combat.Launcher(slot, l, row, now).Loaded;
            if (loaded <= 0 || !Solve(world, ship, row, target, now, truth, out _, out _, out double heading, out _))
                continue;
            double rel = Wrap(heading - ship.HeadingDeg);
            if (!Bears(cls, row, rel))
                continue;
            long at = now;
            if (row.Kind == LauncherKind.Mount && row.Trains)
                store.Combat.Train(slot, row.Index, cls.Mounts[row.Index], now, rel, out at);
            int count = Math.Min(loaded, n - first);
            int p = AllocatePending();
            ref var pe = ref pending[p];
            pe = new Pending
            {
                Alive = true, Generation = pe.Generation + 1, Shooter = shooter, Launcher = l, Target = target, First = first, Count = count, Truth = truth,
            };
            launched[made++] = p;
            first += count;
            world.Schedule(new WorldTime(Math.Max(at, now)), EventTier.Fine, p, Id, LaunchEvent, pe.Generation);
        }
        for (int j = 0; j < made; j++)
            pending[launched[j]].Spread = first;   // the fan is centred on what was fired
        fired |= made > 0;
    }

    /// <summary>Whether a launcher has a launch waiting for its mount.</summary>
    bool Busy(int shooter, int launcher)
    {
        for (int p = 0; p < highPending; p++)
            if (pending[p].Alive && pending[p].Shooter == shooter && pending[p].Launcher == launcher)
                return true;
        return false;
    }

    /// <summary>The aim from a launcher at a target its navy holds in sight (or as it truly is: the dev's): the setting's
    /// speed and range, the course and the run.</summary>
    bool Solve(World world, ShipState ship, in LauncherRow row, int target, long now, bool truth, out double speedMs, out double rangeM,
        out double headingDeg, out double runM)
    {
        (speedMs, rangeM, headingDeg, runM) = (0, 0, double.NaN, double.NaN);
        Vec3d q, v;
        if (truth)
        {
            int ts = ships.Store.SlotOf(target);
            if (ts < 0)
                return false;
            var t = ships.Store[ts];
            (q, v) = (t.Position, t.Forward * t.Speed);
        }
        else
        {
            var pic = world.Sightings.Of(ship.Side);
            int ci = pic?.IndexOf(target) ?? -1;
            if (ci < 0 || !pic![ci].InSight)
                return false;
            (q, v, _) = pic.EstimateAt(ci, now, settings);
        }
        var from = ship.Frame.ToWorld(row.X, row.Y, 0);
        return TorpedoAim.Setting(ship.Handling.Class.Torpedoes[row.Torpedo], from, q, v, out speedMs, out rangeM, out headingDeg, out runM);
    }

    /// <summary>Whether a launcher bears on a relative bearing: a trained one within its arcs, a fixed one within its gyro
    /// angle of its own bearing.</summary>
    bool Bears(ShipClass cls, in LauncherRow row, double relDeg)
    {
        if (row.Trains)
            return CombatStore.InArcs(cls, row.Arcs, relDeg);
        double gyro = settings.TorpedoGyroDeg[Math.Clamp((int)cls.Era, 0, settings.TorpedoGyroDeg.Length - 1)];
        return Math.Abs(Wrap(relDeg - row.RestDeg)) <= gyro;
    }

    int AllocatePending()
    {
        if (freePending.Count > 0)
            return freePending.Pop();
        if (highPending == pending.Length)
            Array.Resize(ref pending, pending.Length * 2);
        return highPending++;
    }

    int Allocate()
    {
        if (freeCount > 0)
            return free[--freeCount];
        if (high == torpedoes.Length)
        {
            Array.Resize(ref torpedoes, torpedoes.Length * 2);
            Array.Resize(ref free, torpedoes.Length);
        }
        return high++;
    }

    // ------------------------------------------------------------------ events

    public void Handle(World world, in SimEvent e)
    {
        if (e.Code == LaunchEvent)
        {
            ref var pe = ref pending[e.Entity];
            if (!pe.Alive || pe.Generation != e.Generation)
                return;
            pe.Alive = false;
            freePending.Push(e.Entity);
            Fire(world, pe);
            return;
        }
        ref var t = ref torpedoes[e.Entity];
        if (!t.Alive || t.Generation != e.Generation)
            return;
        switch (e.Code)
        {
            case CheckEvent:
                Check(world, ref t, e.Entity);
                break;
            case HitEvent:
                Hits++;
                End(world, ref t, e.Entity);
                if (Damage != null && settings.HitsDoDamage && t.ShooterClass >= 0)
                    Damage.Strike(world, new Hit
                    {
                        Ship = t.Hit, Ms = world.Time.Ms, Point = t.HitPoint, Direction = t.HitDirection, SpeedMs = t.HitSpeed, Source = HitSource.Torpedo,
                        Torpedo = ships.Class(t.ShooterClass).Torpedoes[t.Row], ShellType = -1, Shooter = t.Shooter, Battery = t.Launcher,
                    });
                else
                    log.Add(new CombatEntry(world.Time.Ms, CombatEvent.Hit, t.Shooter, t.Launcher, t.Hit, -1, Source: HitSource.Torpedo));
                break;
            case EndEvent:
                Ended++;
                End(world, ref t, e.Entity);
                break;
        }
    }

    /// <summary>A launcher's share of a spread away: the aim solved again now, each torpedo on its own course.</summary>
    void Fire(World world, in Pending pe)
    {
        var store = ships.Store;
        int slot = store.SlotOf(pe.Shooter);
        if (slot < 0)
            return;
        var ship = store[slot];
        var cls = ship.Handling.Class;
        var k = store.Combat.Condition(slot);
        ref readonly var row = ref cls.Launchers[pe.Launcher];
        long now = world.Time.Ms;
        if (k.Status != ShipStatus.Afloat || !k.LauncherWorks(pe.Launcher))
            return;
        int count = Math.Min(pe.Count, store.Combat.Launcher(slot, pe.Launcher, row, now).Loaded);
        if (count <= 0 || !Solve(world, ship, row, pe.Target, now, pe.Truth, out double speed, out double range, out double heading, out double run) ||
            !Bears(cls, row, Wrap(heading - ship.HeadingDeg)))
            return;   // lost it, or it no longer bears: it holds its fire

        var (length, depth) = Target(world, ship.Side, pe.Target, pe.Truth);
        double spacing = settings.TorpedoSpacingShare * length / Math.Max(run, 1);   // radians between neighbours
        int era = Math.Clamp((int)cls.Era, 0, settings.TorpedoFail.Length - 1);
        var origin = ship.Frame.ToWorld(row.X, row.Y, 0).Normalized() * Earth.Radius;
        for (int j = 0; j < count; j++)
        {
            long id = serial;
            var r = stream.Derive((ulong)serial++);
            double off = (pe.First + j - (pe.Spread - 1) / 2.0) * spacing;
            double h = heading + double.RadiansToDegrees(off) + r.NextGaussian() * settings.TorpedoCourseSigmaDeg;
            bool failed = r.NextDouble() < settings.TorpedoFail[era];
            double end = failed ? range * r.NextDouble() : range;
            int i = Allocate();
            ref var t = ref torpedoes[i];
            int gen = t.Generation + 1;
            t = new Torpedo
            {
                Alive = true, Generation = gen, LaunchMs = now, Origin = origin, Forward = Earth.Forward(origin, h), SpeedMs = speed, DepthM = depth,
                EndS = end / speed, Shooter = pe.Shooter, Launcher = pe.Launcher, ShooterClass = ship.ClassIndex, Row = row.Torpedo, Hit = -1,
                Serial = id, Failed = failed,
            };
            alive++;
            world.Schedule(new WorldTime(now), EventTier.Fine, i, Id, CheckEvent, gen);
        }
        store.Combat.Launched(slot, pe.Launcher, row, count, now);
        log.Add(new CombatEntry(now, CombatEvent.Torpedoes, pe.Shooter, pe.Launcher, pe.Target, count, Source: HitSource.Torpedo));
        fired = true;
    }

    /// <summary>A target as its navy takes it: its length (the spread's width) and the depth set for it, from the class
    /// it's taken for, else by its type (the dev's: as it is).</summary>
    (double LengthM, double DepthM) Target(World world, int side, int target, bool truth)
    {
        var pic = world.Sightings.Of(side);
        int ci = pic?.IndexOf(target) ?? -1, ts = ships.Store.SlotOf(target);
        double length = 120, draught = 4;
        if (truth && ts >= 0)
            (length, draught) = (ships.Store[ts].Handling.Class.Length, ships.Store[ts].Handling.Class.Vertical.Draught);
        else if (ci >= 0)
        {
            ref readonly var c = ref pic![ci];
            if (c.TakenFor >= 0 && ships.Registry.Get(c.TakenFor) is { } taken)
                (length, draught) = (taken.Length, taken.Vertical.Draught);
            else
                (length, draught) = c.Type switch
                {
                    ShipType.Capital => (180.0, 9.0),
                    ShipType.Cruiser => (140.0, 6.0),
                    ShipType.Destroyer => (85.0, 3.0),
                    ShipType.Small => (40.0, 1.5),
                    ShipType.Merchant => (120.0, 7.0),
                    _ => (120.0, 4.0),
                };
        }
        return (length, Math.Clamp(settings.TorpedoDepthShare * draught, settings.TorpedoDepthMinM, settings.TorpedoDepthMaxM));
    }

    /// <summary>The next piece of a torpedo's run, swept against the ships near it: a hit at its instant, else the next
    /// piece, or its end.</summary>
    void Check(World world, ref Torpedo t, int i)
    {
        long t0 = world.Time.Ms, endMs = t.LaunchMs + (long)Math.Ceiling(t.EndS * 1000);
        long t1 = Math.Min(t0 + settings.TorpedoCheckMs, endMs);
        double s0 = t.SpeedMs * (t0 - t.LaunchMs) / 1000, s1 = t.SpeedMs * (t1 - t.LaunchMs) / 1000;
        if (s1 > settings.TorpedoArmM && t1 > t0)
        {
            double a = Math.Max(s0, settings.TorpedoArmM);
            long ta = t.LaunchMs + (long)Math.Round(a / t.SpeedMs * 1000);
            var p0 = At(t, a);
            var p1 = At(t, s1);
            var mid = (p0 + p1).Normalized() * Earth.Radius;
            var near = index.Near(mid, (s1 - a) / 2 + biggest + NearMarginM);
            if (near.Length > 0)
            {
                if (candidates.Length < near.Length)
                    candidates = new int[Math.Max(near.Length, candidates.Length * 2)];
                near.CopyTo(candidates);
                if (Sweep(ref t, near.Length, p0, p1, ta, t1, out long hitMs))
                {
                    t.EndS = (hitMs - t.LaunchMs) / 1000.0;
                    world.Schedule(new WorldTime(Math.Max(hitMs, t0)), EventTier.Fine, i, Id, HitEvent, t.Generation);
                    return;
                }
            }
        }
        world.Schedule(new WorldTime(t1), EventTier.Fine, i, Id, t1 >= endMs ? EndEvent : CheckEvent, t.Generation);
    }

    /// <summary>A piece from p0 (at ta) to p1 (at t1), at the torpedo's depth, through each candidate ship in its frame at
    /// the two ends: the first hull or part met, and when.</summary>
    bool Sweep(ref Torpedo t, int n, Vec3d p0, Vec3d p1, long ta, long t1, out long hitMs)
    {
        var store = ships.Store;
        double best = double.PositiveInfinity;
        hitMs = 0;
        long midMs = (ta + t1) / 2;
        var w0 = p0.Normalized() * (Earth.Radius - t.DepthM);
        var w1 = p1.Normalized() * (Earth.Radius - t.DepthM);
        for (int c = 0; c < n; c++)
        {
            int slot = candidates[c];
            var cls = ships.Class(store[slot].ClassIndex);
            var attitude = store.Combat.Condition(slot).Attitude;
            var l0 = HitQuery.ToShip(cls, ships.FrameAt(slot, ta), attitude, w0);
            var l1 = HitQuery.ToShip(cls, ships.FrameAt(slot, t1), attitude, w1);
            if (!HitQuery.Near(cls, l0, l1))
                continue;
            int mounts = cls.Mounts.Length;
            if (bearings.Length < mounts)
                bearings = new double[Math.Max(mounts, bearings.Length * 2)];
            for (int m = 0; m < mounts; m++)
                bearings[m] = CombatStore.BearingAt(store.Combat.Mount(slot, m), cls.Mounts[m], midMs);
            ShipTracer.Trace(cls, l0, l1, bearings.AsSpan(0, mounts), HitQuery.WaterOf(cls, attitude), path);
            double len = Math.Max(path.Length, 1e-9);
            foreach (ref readonly var x in path.Crossings)
                if (x.Kind is CrossingKind.HullIn or CrossingKind.PartIn)
                {
                    double f = x.T / len;
                    if (f < best)
                    {
                        best = f;
                        hitMs = ta + (long)Math.Round((t1 - ta) * f);
                        double dx = l1.X - l0.X, dy = l1.Y - l0.Y, dz = l1.Z - l0.Z;
                        (t.Hit, t.HitPoint, t.HitDirection, t.HitSpeed) = (store[slot].Entity, x.Point, new P3(dx / len, dy / len, dz / len),
                            len / Math.Max((t1 - ta) / 1000.0, 1e-3));
                    }
                    break;
                }
        }
        return best < double.PositiveInfinity;
    }

    /// <summary>Where a torpedo is after a run, m: on the sea's surface over it.</summary>
    static Vec3d At(in Torpedo t, double runM)
    {
        var (p, f) = (t.Origin, t.Forward);
        Earth.Advance(ref p, ref f, runM);
        return p;
    }

    /// <summary>Where a running torpedo is at a moment, its heading there, and how far it has still to run, m (the
    /// captains' lookouts: docs/battle/command.md 5.1a).</summary>
    public static (Vec3d Position, Vec3d Forward, double LeftM) Where(in Torpedo t, long ms)
    {
        double run = Math.Max(0, (ms - t.LaunchMs) / 1000.0) * t.SpeedMs;
        var (p, f) = (t.Origin, t.Forward);
        Earth.Advance(ref p, ref f, run);
        return (p, f, Math.Max(0, t.EndS * t.SpeedMs - run));
    }

    /// <summary>A torpedo's end, a hit or sunk: its mark in the ring the screens read, and its record freed.</summary>
    void End(World world, ref Torpedo t, int i)
    {
        var row = t.ShooterClass >= 0 ? ships.Class(t.ShooterClass).Torpedoes[t.Row] : new TorpedoRow { CalibreMm = 533, ChargeKg = 180 };
        var how = t.Hit >= 0 ? TorpedoEnd.Hit : t.Failed ? TorpedoEnd.Failed : TorpedoEnd.RunOut;
        ends[endNext] = new Ending(At(t, t.SpeedMs * t.EndS), world.Time.Ms, t.Hit, t.Shooter, row.CalibreMm, row.ChargeKg, t.Serial, how);
        endNext = (endNext + 1) % ends.Length;
        endCount = Math.Min(endCount + 1, ends.Length);
        t.Alive = false;
        alive--;
        free[freeCount++] = i;
    }

    static double Wrap(double d) => d - 360 * Math.Round(d / 360);

    // ------------------------------------------------------------------ the rest

    public void Hash(ref StateHash h)
    {
        if (!fired)
            return;   // a world that never fired one hashes as it did before there were torpedoes
        h.Add(alive);
        h.Add(serial);
        for (int i = 0; i < high; i++)
            if (torpedoes[i].Alive)
            {
                ref readonly var t = ref torpedoes[i];
                h.Add(i);
                h.Add(t.LaunchMs);
                h.Add(t.Forward.X);
                h.Add(t.Forward.Y);
                h.Add(t.Forward.Z);
                h.Add(t.SpeedMs);
                h.Add(t.DepthM);
                h.Add(t.EndS);
                h.Add(t.Hit);
            }
        for (int p = 0; p < highPending; p++)
            if (pending[p].Alive)
            {
                h.Add(p);
                h.Add(pending[p].Shooter);
                h.Add(pending[p].Launcher);
                h.Add(pending[p].Target);
                h.Add(pending[p].Count);
                h.Add(pending[p].Truth);
            }
        h.Add(Hits);
        h.Add(Ended);
    }

    public void Fill(Snapshot snap)
    {
        var v = snap.Torpedoes;
        v.EnsureCapacity(alive);
        int n = 0;
        for (int i = 0; i < high; i++)
            if (torpedoes[i].Alive)
            {
                ref readonly var t = ref torpedoes[i];
                (v.Origin[n], v.Forward[n], v.LaunchMs[n], v.SpeedMs[n], v.EndS[n], v.Shooter[n], v.Hit[n]) =
                    (t.Origin, t.Forward, t.LaunchMs, t.SpeedMs, t.EndS, t.Shooter, t.Hit);
                (v.Serial[n], v.DepthM[n], v.Class[n], v.Launcher[n], v.Row[n]) = (t.Serial, t.DepthM, t.ShooterClass, t.Launcher, t.Row);
                n++;
            }
        v.Count = n;
        var w = snap.TorpedoEnds;
        w.EnsureCapacity(endCount);
        n = 0;
        long oldest = snap.Time.Ms - settings.SplashLifeMs;
        for (int k = 0; k < endCount; k++)
        {
            ref readonly var e = ref ends[(endNext - 1 - k + ends.Length) % ends.Length];
            if (e.Ms < oldest)
                break;
            (w.Point[n], w.Ms[n], w.Ship[n], w.Shooter[n], w.CalibreMm[n]) = (e.Point, e.Ms, e.Ship, e.Shooter, e.CalibreMm);
            (w.ChargeKg[n], w.Serial[n], w.How[n]) = (e.ChargeKg, e.Serial, e.How);
            n++;
        }
        w.Count = n;
    }
}
