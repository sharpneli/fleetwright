using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim;

/// <summary>What the gunnery system understands (<see cref="GunnerySystem"/>).</summary>
public enum GunneryCommand : ushort
{
    Engage = 1,
    Cease = 2,
    SetCrew = 3,
}

/// <summary>What an engaged battery is doing.</summary>
public enum BatteryState : byte
{
    /// <summary>Its navy doesn't hold the target in sight, and it has no track yet.</summary>
    NoContact,
    /// <summary>Tracking: readings into the computer, the guns not yet opened.</summary>
    Tracking,
    Firing,
    /// <summary>No mount of it has the target in its arcs.</summary>
    NotBearing,
    /// <summary>The target is beyond the guns' reach.</summary>
    OutOfRange,
    /// <summary>Its ship isn't on the fine tick: guns are silent in the coarse tier until 4.9 (fighting.md decision 4).</summary>
    Silent,
}

/// <summary>The batteries' fire control and firing (docs/battle/fighting.md 4.2b; a port of
/// research/simulation/fire_control_ref.py <c>engage()</c> cut to its spine, one cause of a miss a stage):
/// <list type="bullet">
/// <item><b>Measure.</b> Every <see cref="SimSettings.FireControlMs"/> on a grid of world time, each engaged battery's
/// rangefinders (its directors' eyes; without a director, a mount's layer by eye) take readings at their level's rate
/// while they see the target: the range's error δ·R²/(B·M) (by eye ~20%), each instrument's own bias drawn at the
/// engagement's start, the bearing's in mil. A reading reaches the computer after the level's latency.</item>
/// <item><b>Track.</b> A constant-velocity Kalman track (<see cref="Track"/>, as the pictures keep), started from the
/// navy's picture of the target, so acquiring costs nothing new; the level's q. One tracker for every level for now
/// (the Dreyer's polar drift, the turn models: 4.5).</item>
/// <item><b>Solve.</b> The target where it'll be after the time of flight, from the track (which lags by the latency),
/// the gun laid for it with its ship's motion in (<see cref="FireControl.Solve"/>), plus the engagement's residual bias
/// and the spotting corrections.</item>
/// <item><b>Lay, fire.</b> Mounts train and elevate as analytic anchors (<see cref="CombatStore"/>); a salvo goes at the
/// first instant the bearing mounts are loaded and laid. Each salvo draws one common error (the level's salvo-to-salvo
/// error, times battle's <see cref="SimSettings.GunneryCombat"/>; the director's laying error, or each gun's for a
/// battery without director firing), each shell its own dispersion from the gun's pattern; a ship swinging adds the
/// level's own-turn kick.</item>
/// <item><b>Spot.</b> When a salvo's last shell is down, and <see cref="SimSettings.SpotDelayMs"/> on, the spotter (the
/// highest eye) judges it over, short or straddling (σ = k·R²/h) and sees its deflection; the RN's ladder corrects the
/// next salvos, against the correction that salvo was fired with.</item>
/// </list>
/// Hits are counted and logged but do nothing yet (4.3a). Batteries fire only on the fine tick. World state: replays,
/// hashes. Registered fourth, so its id is always <see cref="Id"/>.</summary>
public sealed class GunnerySystem : IWorldSystem
{
    public const byte Id = 4;
    const ushort TickEvent = 1, SalvoEvent = 2, SpotEvent = 3;
    const int MaxSensors = 4, PendingCap = 8;

    /// <summary>A battery's fire control while it's engaged.</summary>
    struct Fc
    {
        public bool Alive;
        public int Generation;
        public int Shooter, Battery, Target, Level, Table;
        public bool Local;
        /// <summary>The bearing (from ahead) and elevation the mounts were last ordered to, degrees; the last solution's
        /// elevation in the plane of fire and time of flight (where the next second's starts).</summary>
        public double OrderBearingDeg, OrderElevationDeg, ElevationDeg, TofS;
        public long StartMs, OpenMs, TrackMs;
        public bool Tracking, SalvoPending;
        public Track Track;
        public double BiasR, BiasD, CorrR, CorrD, SpotHeightM;
        public FireControl.Ladder Ladder;
        public int Salvos, Shells, Hits, Straddles, Arm, SensorCount, PendHead, PendCount;
        /// <summary>The shooter's condition's version its instruments were picked at (damage: a director lost).</summary>
        public int Condition;
        public long FirstHitMs, FirstStraddleMs;
        public BatteryState State;
    }

    /// <summary>A rangefinder or an eye a battery reads from: the class's eye (-1: a mount's layer), where it stands,
    /// its height and base, its next reading and its own bias (in sigmas).</summary>
    struct Sensor
    {
        public int Eye;
        public double X, Y, HeightM, BaseM, Bias;
        public long NextMs;
    }

    /// <summary>A reading on its way to the computer.</summary>
    struct Reading
    {
        public long ArriveMs, Ms;
        public Vec3d Z;
        public double R11, R12, R22;
    }

    /// <summary>A salvo in the air, for its spot: whose, at whom, the corrections it was fired with, its pattern and line
    /// of fire, and its fall so far.</summary>
    struct Salvo
    {
        public bool Alive;
        public int Generation, Fc, FcGeneration, Target, Shells, Left, Landed, Over, Short, Hits;
        public double CorrR, CorrD, PatternM, RangeM, SpotSigmaM, Common, SumY, MinX, MaxX, TargetLengthM, SumX, AimErrM, AimErrAcrossM;
        public long FiredMs;
        public Vec3d U, W;
    }

    /// <summary>A salvo's fall, for tools (not world state): when it was fired and by whom, its range and shells, where
    /// its shells fell on average against the target (along the line of fire and to its right, m), how far the aim
    /// point was from where the target truly went (the track's error, as the target's course and speed carry it on),
    /// the range correction it was fired with, its hits, and whether it straddled.</summary>
    public readonly record struct SalvoReport(long FiredMs, int Shooter, int Battery, double RangeM, int Shells, double MeanX, double MeanY, double AimErrM,
        double AimErrAcrossM, double CorrR, int Hits, bool Straddle);

    /// <summary>Told of each salvo once it's down (tools and benches; the world never reads it).</summary>
    public Action<SalvoReport>? Reported { get; set; }

    readonly SimSettings settings;
    readonly ShipSystem ships;
    readonly ShellSystem shells;
    readonly CombatLog log;
    readonly RandomStream stream;
    Fc[] fcs = new Fc[16];
    Sensor[] sensors = new Sensor[16 * MaxSensors];
    Reading[] pending = new Reading[16 * PendingCap];
    readonly Stack<int> freeFc = new(64);
    int highFc;
    Salvo[] salvos;
    readonly Stack<int> freeSalvo;
    int highSalvo;
    bool ticking, ever;
    readonly CrewQuality[] crews = new CrewQuality[256];
    bool crewsSet;
    bool[] firing = new bool[64];
    int[] fireLoad = new int[64];

    internal GunnerySystem(SimSettings settings, ShipSystem ships, ShellSystem shells, CombatLog log, ulong seed)
    {
        (this.settings, this.ships, this.shells, this.log) = (settings, ships, shells, log);
        stream = RandomStream.Named(seed, "gunnery");
        salvos = new Salvo[Math.Max(settings.SalvoCapacity, 16)];
        freeSalvo = new Stack<int>(salvos.Length);   // as big as the pool, so freeing never grows it
        Array.Fill(crews, CrewQuality.Trained);
    }

    // ------------------------------------------------------------------ commands

    /// <summary>A ship's battery (or all of them: -1) engages a ship (an entity). A level of -1 is the battery's own; a
    /// dev or a bench may try another (0-8).</summary>
    public static Command Engage(int shooter, int battery, int target, int level = -1) =>
        new(Id, (ushort)GunneryCommand.Engage, shooter, A: target, B: battery, X: level);

    /// <summary>A ship's battery (or all: -1) ceases fire.</summary>
    public static Command Cease(int shooter, int battery = -1) => new(Id, (ushort)GunneryCommand.Cease, shooter, B: battery);

    /// <summary>A side's gun crews (the scenario's to give; trained by default).</summary>
    public static Command SetCrew(int side, CrewQuality quality) => new(Id, (ushort)GunneryCommand.SetCrew, side, A: (long)quality);

    public CrewQuality Crew(int side) => crews[side & 255];

    readonly Dictionary<(int Table, double Elevation), double> reach = [];

    /// <summary>How far a ship's battery reaches with its loadout's lead shell: its range table's fall at its mounts'
    /// highest elevation from a muzzle 10 m up, m (worked out once per gun and elevation).</summary>
    public double ReachM(Loadout lo, int battery)
    {
        var cls = lo.Class;
        double top = 0;
        foreach (int m in cls.Links(cls.Batteries[battery].Mounts))
            top = Math.Max(top, cls.Mounts[m].MaxElevationDeg);
        int table = shells.TableOf(lo, battery);
        if (!reach.TryGetValue((table, top), out double r))
        {
            var t = shells.Ballistics[table];
            reach[(table, top)] = r = t.Fall(Math.Min(top, t.MaxElevationDeg), 10).Range;
        }
        return r;
    }

    public void Apply(World world, in Command c)
    {
        switch ((GunneryCommand)c.Code)
        {
            case GunneryCommand.Engage:
                EngageNow(world, c.Entity, c.B, (int)c.A, (int)Math.Round(c.X));
                break;
            case GunneryCommand.Cease:
                for (int i = 0; i < highFc; i++)
                    if (fcs[i].Alive && fcs[i].Shooter == c.Entity && (c.B < 0 || fcs[i].Battery == c.B))
                        Free(i);
                break;
            case GunneryCommand.SetCrew:
                if (c.Entity is < 0 or > 255 || c.A is < 0 or > (long)CrewQuality.Elite)
                    throw new ArgumentException($"no crew {c.A} for side {c.Entity}");
                crews[c.Entity] = (CrewQuality)c.A;
                crewsSet = true;
                break;
        }
    }

    void EngageNow(World world, int shooter, int battery, int target, int level)
    {
        var store = ships.Store;
        int slot = store.SlotOf(shooter);
        if (slot < 0 || store.SlotOf(target) < 0 || target == shooter)
            return;
        var cls = ships.Class(store[slot].ClassIndex);
        if (battery >= cls.Batteries.Length)
            return;
        long now = world.Time.Ms;
        for (int b = Math.Max(battery, 0); b < (battery < 0 ? cls.Batteries.Length : battery + 1); b++)
        {
            for (int i = 0; i < highFc; i++)
                if (fcs[i].Alive && fcs[i].Shooter == shooter && fcs[i].Battery == b)
                    Free(i);
            int f = AllocateFc();
            ref var fc = ref fcs[f];
            ref readonly var row = ref cls.Batteries[b];
            int gen = fc.Generation + 1;
            int lv = level >= 0 ? level : row.Local ? 0 : (int)row.Level;
            fc = new Fc
            {
                Alive = true, Generation = gen, Shooter = shooter, Battery = b, Target = target, StartMs = now,
                Level = Math.Clamp(lv, 0, settings.GunneryLevels.Length - 1), Local = row.Local, FirstHitMs = -1, FirstStraddleMs = -1,
                Table = shells.TableOf(store.Combat.Loadout(slot), b), ElevationDeg = double.NaN,
            };
            var lvl = settings.GunneryLevels[fc.Level];
            fc.Ladder.Step = lvl.Ladder0M;
            var r = stream.Derive((ulong)f).Derive((ulong)gen);
            fc.BiasR = r.NextGaussian();
            fc.BiasD = r.NextGaussian();
            AddSensors(ref fc, f, cls, row, store.Combat.Condition(slot), now, lvl, ref r);
            store.Combat.Engage(slot, b, target);
        }
        ever = true;
        if (store[slot].Tier != ClockTier.Fine && store[slot].OnRails)
            world.Ships.Derail(world, shooter);
        Tick(world, schedule: true);
    }

    /// <summary>A battery's rangefinders: its working directors' eyes, longest base first (as many as
    /// <see cref="MaxSensors"/>); without one, the highest of its working mounts' layers by eye, at local control.</summary>
    void AddSensors(ref Fc fc, int f, ShipClass cls, in BatteryRow row, ShipCondition cond, long now, GunneryLevel lvl, ref RandomStream r)
    {
        int n = 0;
        fc.Condition = cond.Version;
        if (!row.Local)
            foreach (int d in cls.Links(row.Directors))
            {
                var eyes = cls.Eyes;
                if (!cond.DirectorWorks[d])
                    continue;
                for (int e = 0; e < eyes.Length && n < MaxSensors; e++)
                    if (eyes[e].Kind == EyeKind.Director && eyes[e].Component == cls.Directors[d].Component && cond.EyeWorks[e])
                    {
                        sensors[f * MaxSensors + n++] = new Sensor
                        {
                            Eye = e, X = eyes[e].X, Y = eyes[e].Y, HeightM = eyes[e].HeightM, BaseM = eyes[e].RangefinderM,
                        };
                        break;
                    }
            }
        if (n == 0)
        {
            // local control: the highest working mount's layer, by eye (any mount's, when none works)
            double best = double.MinValue;
            bool works = false;
            var s = new Sensor { Eye = -1 };
            foreach (int m in cls.Links(row.Mounts))
            {
                ref readonly var comp = ref cls.Components[cls.Mounts[m].Component];
                double h = comp.Bounds.Top - cls.Vertical.Waterline;
                bool w = cond.MountWorks[m];
                if (w && !works || w == works && h > best)
                    (best, works, s.X, s.Y, s.HeightM) = (h, w, cls.Mounts[m].X, cls.Mounts[m].Y, h);
            }
            sensors[f * MaxSensors + n++] = s;
            if (!fc.Local)
                fc.Level = 0;   // its directors gone: each mount for itself, by eye (the level local control fires at)
            fc.Local = true;
        }
        fc.SensorCount = n;
        fc.SpotHeightM = 0;
        for (int k = 0; k < n; k++)
        {
            ref var s = ref sensors[f * MaxSensors + k];
            s.Bias = r.NextGaussian();
            s.NextMs = now + (long)(r.NextDouble() * 60_000 / Math.Max(Rate(lvl, s), 1e-6));
            fc.SpotHeightM = Math.Max(fc.SpotHeightM, s.HeightM);
        }
    }

    /// <summary>A sensor's readings a minute: an eye one, a rangefinder the level's.</summary>
    static double Rate(GunneryLevel lvl, in Sensor s) => Ranges(lvl, s) ? lvl.ReadingsPerMin : 1;

    /// <summary>Whether a sensor takes ranges with its rangefinder: it has one, and the level has the instruments to
    /// read it (row 0, the eyeball, hasn't: a director's rangefinder at level 0 ranges by eye).</summary>
    static bool Ranges(GunneryLevel lvl, in Sensor s) => s.BaseM > 0 && lvl.Magnification > 0;

    int AllocateFc()
    {
        if (freeFc.Count > 0)
            return freeFc.Pop();
        if (highFc == fcs.Length)
        {
            Array.Resize(ref fcs, fcs.Length * 2);
            Array.Resize(ref sensors, fcs.Length * MaxSensors);
            Array.Resize(ref pending, fcs.Length * PendingCap);
        }
        return highFc++;
    }

    void Free(int f)
    {
        ref var fc = ref fcs[f];
        if (!fc.Alive)
            return;
        int slot = ships.Store.SlotOf(fc.Shooter);
        if (slot >= 0)
            ships.Store.Combat.Engage(slot, fc.Battery, 0);
        fc.Alive = false;
        fc.Generation++;   // its salvo and spot events go stale
        freeFc.Push(f);
    }

    // ------------------------------------------------------------------ events

    public void Handle(World world, in SimEvent e)
    {
        switch (e.Code)
        {
            case TickEvent:
                ticking = false;
                Tick(world, schedule: false);
                break;
            case SalvoEvent:
                if (fcs[e.Entity].Alive && fcs[e.Entity].Generation == e.Generation && fcs[e.Entity].Arm == e.A)
                    Fire(world, e.Entity);
                break;
            case SpotEvent:
                if (salvos[e.Entity].Alive && salvos[e.Entity].Generation == e.Generation)
                    Spot(world, e.Entity);
                break;
        }
    }

    /// <summary>The fire-control tick: each engaged battery measured, tracked, its mounts ordered and its next salvo
    /// timed; the next tick on the grid while any battery is engaged. From a command (<paramref name="schedule"/>), only
    /// the next tick is scheduled.</summary>
    void Tick(World world, bool schedule)
    {
        long now = world.Time.Ms, step = settings.FireControlMs;
        bool any = false;
        for (int f = 0; f < highFc; f++)
            if (fcs[f].Alive)
            {
                if (!schedule)
                    Run(world, f, now);
                any |= fcs[f].Alive;
            }
        if (any && !ticking)
        {
            long next = schedule ? (now + step - 1) / step * step : now + step;
            world.Schedule(new WorldTime(next), EventTier.Fine, 0, Id, TickEvent);
            ticking = true;
        }
    }

    /// <summary>One battery's second: readings taken and taken in, the solution, the mounts ordered, the salvo timed.</summary>
    void Run(World world, int f, long now)
    {
        ref var fc = ref fcs[f];
        var store = ships.Store;
        int slot = store.SlotOf(fc.Shooter), tslot = store.SlotOf(fc.Target);
        if (slot < 0 || tslot < 0)
        {
            Free(f);
            return;
        }
        var ship = store[slot];
        if (ship.Tier != ClockTier.Fine)
        {
            fc.State = BatteryState.Silent;
            return;
        }
        var cls = ship.Handling.Class;
        var target = store[tslot];
        var cond = store.Combat.Condition(slot);
        if (cond.Version != fc.Condition)
        {
            // damage: its instruments picked again from what still works
            var rs = stream.Derive((ulong)f).Derive((ulong)fc.Generation).Derive(0xDA3u).Derive((ulong)cond.Version);
            AddSensors(ref fc, f, cls, cls.Batteries[fc.Battery], cond, now, settings.GunneryLevels[fc.Level], ref rs);
        }
        var lvl = settings.GunneryLevels[fc.Level];
        var crew = settings.GunneryCrews[(int)crews[ship.Side]];
        var pic = world.Sightings.Of(ship.Side);
        int ci = pic?.IndexOf(fc.Target) ?? -1;
        bool inSight = ci >= 0 && pic![ci].InSight;
        if (!fc.Tracking)
        {
            if (!inSight)
            {
                fc.State = BatteryState.NoContact;
                return;
            }
            // acquired from the navy's picture: its estimate, the velocity no surer than the eye's first guess
            var (p, v, cov) = pic!.EstimateAt(ci, now, settings);
            double kn = lvl.PriorKn * Handling.MsPerKnot, sv = kn * kn + Math.Pow(v.Length * double.DegreesToRadians(lvl.PriorDeg), 2);
            (cov.VeVe, cov.VnVn) = (Math.Max(cov.VeVe, sv), Math.Max(cov.VnVn, sv));
            fc.Track = new Track { Position = p, Velocity = v, P = cov, Ms = now };
            (fc.Tracking, fc.TrackMs) = (true, now);
            fc.OpenMs = now + (long)((lvl.LatencyS + settings.GunneryOpenS) * 1000);
        }

        // measure: each sensor that's due and sees it
        var frame = ship.Frame;
        var tpos = target.Position;
        double tTop = target.Handling.Class.Silhouette.TopM, combat = settings.GunneryCombat;
        bool sees = false;
        for (int k = 0; k < fc.SensorCount; k++)
        {
            ref var s = ref sensors[f * MaxSensors + k];
            var at = frame.ToWorld(s.X, s.Y, 0);
            bool seen = inSight && Sees(cls, s, at, ship.HeadingDeg, tpos, tTop);
            sees |= seen;
            if (now < s.NextMs)
                continue;
            var r = stream.Derive((ulong)f).Derive((ulong)fc.Generation).Derive((ulong)now).Derive((ulong)k);
            s.NextMs = now + (long)(60_000 / Rate(lvl, s) * r.NextDouble(0.7, 1.3));
            if (!seen)
                continue;
            double range = Earth.Distance(at, tpos), brg = Earth.Bearing(at, tpos), sr, bias, sb;
            if (Ranges(lvl, s))
            {
                double unit = FireControl.RangefinderUnitM(s.BaseM, lvl.Magnification, range, settings);
                (sr, bias, sb) = (crew.Operator * combat * unit, s.Bias * settings.RangefinderBiasUnits * unit, settings.RangefinderBearingMil * FireControl.Mil);
            }
            else
                (sr, bias, sb) = (settings.EyeRangeShare * range * crew.Operator * combat, s.Bias * settings.EyeRangeBiasShare * range,
                    settings.EyeBearingMil * FireControl.Mil);
            double zr = Math.Max(range + bias + sr * r.NextGaussian(), 100), zb = brg + double.RadiansToDegrees(sb * r.NextGaussian());
            var (z, r11, r12, r22) = Track.Reading(at, zr, zb, sr, sb);
            if (fc.PendCount == PendingCap)
                PopReading(ref fc, f, lvl);   // the computer's queue is full: the oldest goes in now
            int at2 = f * PendingCap + (fc.PendHead + fc.PendCount++) % PendingCap;
            pending[at2] = new Reading { ArriveMs = now + (long)(lvl.LatencyS * 1000), Ms = now, Z = z, R11 = r11, R12 = r12, R22 = r22 };
        }

        // the computer: readings that have come through
        while (fc.PendCount > 0 && pending[f * PendingCap + fc.PendHead].ArriveMs <= now)
            PopReading(ref fc, f, lvl);

        // solve, and order the mounts
        ref readonly var row = ref cls.Batteries[fc.Battery];
        var mounts = cls.Links(row.Mounts);
        var table = shells.Ballistics[fc.Table];
        var muzzle = Muzzle(cls, frame, mounts[0]);
        var aim = Predict(ref fc, table, muzzle, now, lvl);
        bool laid = FireControl.Solve(table, muzzle, ship.Forward * ship.Speed, aim, out var lay, fc.ElevationDeg);
        double bearing = Mod((laid ? Earth.Heading(muzzle, lay.GunHorizontal) : Earth.Bearing(ship.Position, aim)) - ship.HeadingDeg);
        double elevation = laid ? lay.GunElevationDeg : double.PositiveInfinity;
        (fc.OrderBearingDeg, fc.OrderElevationDeg) = (bearing, elevation);
        if (firing.Length < mounts.Length)
            firing = new bool[Math.Max(mounts.Length, firing.Length * 2)];
        bool bears = false;
        for (int j = 0; j < mounts.Length; j++)
        {
            ref readonly var mr = ref cls.Mounts[mounts[j]];
            bool works = cond.MountWorks[mounts[j]];   // one out of action or jammed stays where it stopped
            bool inTraverse = works && store.Combat.Order(slot, mounts[j], mr, now, bearing, elevation);
            firing[j] = inTraverse && CombatStore.InArcs(cls, mr, bearing) && store.Combat.Mount(slot, mounts[j]).Rounds > 0 &&
                        elevation <= mr.MaxElevationDeg;
            bears |= firing[j];
        }
        fc.State = !laid ? BatteryState.OutOfRange : !bears ? BatteryState.NotBearing : now < fc.OpenMs ? BatteryState.Tracking : BatteryState.Firing;

        // time the salvo: the first instant the bearing mounts are loaded and laid
        if (fc.State != BatteryState.Firing || fc.SalvoPending || !sees)
            return;
        long when = now;
        for (int j = 0; j < mounts.Length; j++)
            if (firing[j])
            {
                ref readonly var ms = ref store.Combat.Mount(slot, mounts[j]);
                when = Math.Max(when, Math.Max(ms.LoadedMs, CombatStore.LaidBy(ms, cls.Mounts[mounts[j]])));
            }
        if (when < now + settings.FireControlMs)
        {
            fc.SalvoPending = true;
            fc.Arm++;
            world.Schedule(new WorldTime(when), EventTier.Fine, f, Id, SalvoEvent, fc.Generation, fc.Arm);
        }
    }

    /// <summary>Whether a battery's sensor sees the target: its eye bears on it (no blind sector), and it's over the
    /// horizon from the eye's height, within the day's visibility.</summary>
    bool Sees(ShipClass cls, in Sensor s, Vec3d at, double headingDeg, Vec3d target, double targetTopM)
    {
        double d = Earth.Distance(at, target);
        if (d > SightModel.HorizonM(s.HeightM, settings) + SightModel.HorizonM(targetTopM, settings))
            return false;
        return s.Eye < 0 || cls.Eyes[s.Eye].Sees(Earth.Bearing(at, target) - headingDeg);
    }

    void PopReading(ref Fc fc, int f, GunneryLevel lvl)
    {
        ref readonly var r = ref pending[f * PendingCap + fc.PendHead];
        fc.Track.Fix(r.Ms, lvl.Q, r.Z, r.R11, r.R12, r.R22);
        fc.PendHead = (fc.PendHead + 1) % PendingCap;
        fc.PendCount--;
    }

    /// <summary>Where the track says the target will be when a shell fired now from a muzzle comes down: the time of
    /// flight iterated through the range table, from last second's.</summary>
    Vec3d Predict(ref Fc fc, BallisticTable table, Vec3d muzzle, long now, GunneryLevel lvl)
    {
        double h0 = muzzle.Length - Earth.Radius;
        var p = fc.Track.At(now + (long)(fc.TofS * 1000), lvl.Q).Position;
        for (int i = 0, n = fc.TofS > 0 ? 2 : 3; i < n; i++)
        {
            double e = table.ElevationFor(Earth.Distance(muzzle, p), h0, 1, fc.ElevationDeg);
            if (double.IsNaN(e))
                break;
            fc.ElevationDeg = e;
            fc.TofS = table.Fall(e, h0).Tof;
            p = fc.Track.At(now + (long)(fc.TofS * 1000), lvl.Q).Position;
        }
        return p;
    }

    /// <summary>A mount's muzzle in the world: its guns' axis over the sea (as Armament.BarrelBand has it).</summary>
    static Vec3d Muzzle(ShipClass cls, in ShipFrame frame, int mount)
    {
        ref readonly var m = ref cls.Mounts[mount];
        ref readonly var comp = ref cls.Components[m.Component];
        double axis = comp.Bounds.Base + 0.55 * (comp.Bounds.Top - comp.Bounds.Base);
        return frame.ToWorld(m.X, m.Y, axis - cls.Vertical.Waterline);
    }

    /// <summary>A salvo: the bearing mounts that are loaded and laid fire together, each shell laid for its own point
    /// (the aim, the salvo's common error, its own dispersion).</summary>
    void Fire(World world, int f)
    {
        ref var fc = ref fcs[f];
        fc.SalvoPending = false;
        var store = ships.Store;
        int slot = store.SlotOf(fc.Shooter), tslot = store.SlotOf(fc.Target);
        if (slot < 0 || tslot < 0 || store[slot].Tier != ClockTier.Fine)
            return;
        long now = world.Time.Ms;
        var ship = store[slot];
        var cls = ship.Handling.Class;
        ref readonly var row = ref cls.Batteries[fc.Battery];
        var mounts = cls.Links(row.Mounts);
        var cond = store.Combat.Condition(slot);
        var loads = store.Combat.Loadout(slot);
        if (firing.Length < mounts.Length)
            firing = new bool[Math.Max(mounts.Length, firing.Length * 2)];
        if (fireLoad.Length < mounts.Length)
            fireLoad = new int[Math.Max(mounts.Length, fireLoad.Length * 2)];
        int n = 0, lead = -1;
        for (int j = 0; j < mounts.Length; j++)
        {
            ref readonly var mr = ref cls.Mounts[mounts[j]];
            ref readonly var ms = ref store.Combat.Mount(slot, mounts[j]);
            // loaded, with rounds; ordered on the target (not parked at its traverse's end) inside its arcs; and there
            double tol = settings.LayToleranceDeg;
            firing[j] = ms.LoadedMs <= now && ms.Rounds > 0 && cond.MountWorks[mounts[j]] && fc.OrderElevationDeg <= mr.MaxElevationDeg && CombatStore.InArcs(cls, mr, fc.OrderBearingDeg) &&
                        Math.Abs(Wrap(ms.TrainTo - fc.OrderBearingDeg)) <= tol &&
                        Math.Abs(CombatStore.BearingAt(ms, mr, now) - ms.TrainTo) <= tol && Math.Abs(CombatStore.ElevationAt(ms, mr, now) - ms.ElevTo) <= tol;
            // each mount fires the first load of the magazine order it has rounds of
            fireLoad[j] = firing[j] ? store.Combat.NextLoad(slot, mounts[j]) : -1;
            firing[j] &= fireLoad[j] >= 0;
            if (firing[j])
            {
                n += cls.TurretTypes[mr.Type].Barrels;
                if (lead < 0)
                    lead = loads.Loads(fc.Battery)[fireLoad[j]].ShellType;
            }
        }
        if (n == 0)
            return;
        // the battery's computer works in its lead shell's table: the first firing mount's (the next salvos lay with it)
        fc.Table = shells.TableOf(loads, fc.Battery, lead);

        var lvl = settings.GunneryLevels[fc.Level];
        var crew = settings.GunneryCrews[(int)crews[ship.Side]];
        int tableIndex = fc.Table;
        var table = shells.Ballistics[tableIndex];
        var frame = ship.Frame;
        var velocity = ship.Forward * ship.Speed;
        var muzzle0 = Muzzle(cls, frame, mounts[0]);
        double h0 = muzzle0.Length - Earth.Radius;
        var p = Predict(ref fc, table, muzzle0, now, lvl);
        var up = p.Normalized();
        var line = p - ship.Position;
        var u = (line - up * Vec3d.Dot(line, up)).Normalized();   // along the line of fire, at the target
        var w = Vec3d.Cross(u, up);                                  // to its right
        double range = Earth.Distance(ship.Position, p), combat = settings.GunneryCombat;
        double e0 = table.ElevationFor(range, h0, 1, fc.ElevationDeg);
        if (double.IsNaN(e0))
            return;
        double perRad = table.RangePerDeg(e0, h0) * 180 / Math.PI;

        // the salvo's common error (fire_control_ref.py engage(), "fire")
        var r = stream.Derive((ulong)f).Derive((ulong)fc.Generation).Derive(0x5A1u).Derive((ulong)fc.Salvos);
        double layRad = lvl.LayMil * FireControl.Mil * crew.Lay * combat;
        double eEl = lvl.Director ? r.NextGaussian() * layRad * perRad : 0, eTr = lvl.Director ? r.NextGaussian() * layRad * range : 0;
        bool turning = Math.Abs(double.RadiansToDegrees(ship.YawRate)) > settings.OwnTurnYawDegS;
        double kickR = turning ? r.NextGaussian() * lvl.OwnTurnKickM : 0, kickL = turning ? r.NextGaussian() * lvl.OwnTurnKickM : 0;
        double eSr = r.NextGaussian() * combat * lvl.SalvoPct / 100 * range, eSd = r.NextGaussian() * combat * lvl.SalvoDeflMil * FireControl.Mil * range;
        double dr = fc.CorrR + eEl + eSr + fc.BiasR * lvl.ResidPct / 100 * range + kickR;
        double dl = fc.CorrD + eTr + eSd + fc.BiasD * lvl.DeflBiasMil * FireControl.Mil * range + kickL;
        var (sr, sd) = FireControl.Dispersion(row.PatternPct, range + dr, n, settings.DispersionDeflectionRatio);
        if (!lvl.Director)
            (sr, sd) = (Math.Sqrt(sr * sr + Math.Pow(layRad * perRad, 2)), Math.Sqrt(sd * sd + Math.Pow(layRad * range, 2)));
        var centre = p + u * dr + w * dl;
        // the aim's error against the truth, for the report: where the target goes on its course and speed meanwhile
        var tgt = store[tslot];
        double tof = table.Fall(e0, h0).Tof;
        var truth = tgt.Position;
        var tf = tgt.Forward;
        Earth.Advance(ref truth, ref tf, tgt.Speed * tof);

        // its record, for the spot
        int si = AllocateSalvo();
        ref var sv = ref salvos[si];
        double spot = settings.SpotOpticK * combat * range * range / Math.Max(fc.SpotHeightM, 1);
        sv = new Salvo
        {
            Alive = true, Generation = sv.Generation + 1, Fc = f, FcGeneration = fc.Generation, Target = fc.Target, CorrR = fc.CorrR, CorrD = fc.CorrD,
            PatternM = 4 * sr, RangeM = range, SpotSigmaM = spot, Common = 0.5 * spot * r.NextGaussian(), U = u, W = w, MinX = double.MaxValue,
            MaxX = double.MinValue, TargetLengthM = store[tslot].Handling.Class.Silhouette.LengthM, FiredMs = now,
            AimErrM = Vec3d.Dot(p - truth, u), AimErrAcrossM = Vec3d.Dot(p - truth, w),
        };
        int salvo = shells.NewSalvo();
        for (int j = 0; j < mounts.Length; j++)
        {
            if (!firing[j])
                continue;
            ref readonly var mr = ref cls.Mounts[mounts[j]];
            int barrels = cls.TurretTypes[mr.Type].Barrels;
            var muzzle = Muzzle(cls, frame, mounts[j]);
            // its own shell, laid with its own table (a mount out of the lead's kind fires its next one in the salvo)
            int type = loads.Loads(fc.Battery)[fireLoad[j]].ShellType, own = type == lead ? tableIndex : shells.TableOf(loads, fc.Battery, type);
            var ownTable = shells.Ballistics[own];
            int away = 0;
            Vec3d first = default;
            double firstEl = 0;
            for (int g = 0; g < barrels; g++)
            {
                var point = centre + u * (sr * r.NextGaussian()) + w * (sd * r.NextGaussian());
                point = point.Normalized() * Earth.Radius;
                if (FireControl.Solve(ownTable, muzzle, velocity, point, out var lay, e0) &&
                    shells.Launch(world, own, muzzle, lay.Forward, lay.ElevationDeg, lay.Speed, salvo, fc.Shooter, fc.Battery, si, type, loads))
                {
                    sv.Shells++;
                    if (away++ == 0)
                        (first, firstEl) = (lay.Forward, lay.ElevationDeg);
                }
            }
            if (away > 0)
                shells.Fired(world, fc.Shooter, ship.ClassIndex, mounts[j], salvo, away, muzzle, first, firstEl);
            store.Combat.Fired(slot, cls, mounts[j], fireLoad[j], barrels, now);
        }
        sv.Left = sv.Shells;
        if (sv.Shells == 0)
        {
            FreeSalvo(si);   // no shell got away (every one laid past its reach): no salvo
            return;
        }
        fc.Salvos++;
        fc.Shells += sv.Shells;
        log.Add(new CombatEntry(now, CombatEvent.Fired, fc.Shooter, fc.Battery, fc.Target, sv.Shells));
    }

    int AllocateSalvo()
    {
        if (freeSalvo.Count > 0)
            return freeSalvo.Pop();
        if (highSalvo == salvos.Length)
            Array.Resize(ref salvos, salvos.Length * 2);
        return highSalvo++;
    }

    void FreeSalvo(int i)
    {
        salvos[i].Alive = false;
        freeSalvo.Push(i);
    }

    /// <summary>A shell of a salvo down (from the shell system, at its end): where it fell against the target, as it
    /// is and as the spotter will judge it; the last one schedules the spot.</summary>
    internal void Landed(World world, int si, Vec3d end, int hit)
    {
        ref var sv = ref salvos[si];
        if (!sv.Alive)
            return;
        var store = ships.Store;
        int tslot = store.SlotOf(sv.Target);
        if (tslot >= 0)
        {
            var d = end - store[tslot].Position;
            double x = Vec3d.Dot(d, sv.U), y = Vec3d.Dot(d, sv.W);
            var r = stream.Derive(0x5B07u).Derive((ulong)si).Derive((ulong)sv.Generation).Derive((ulong)sv.Landed);
            double seen = hit == sv.Target ? 0 : x + sv.Common + sv.SpotSigmaM * r.NextGaussian();
            if (seen > 0)
                sv.Over++;
            else if (seen < 0)
                sv.Short++;
            if (hit == sv.Target)
                sv.Hits++;
            sv.SumY += y;
            sv.SumX += x;
            (sv.MinX, sv.MaxX) = (Math.Min(sv.MinX, x), Math.Max(sv.MaxX, x));
        }
        sv.Landed++;
        if (--sv.Left > 0)
            return;
        // down: the truth for the record, and the spot to come
        ref var fc = ref fcs[sv.Fc];
        bool straddle = sv.Hits > 0 || sv.MinX < 0 && sv.MaxX > 0 && Math.Abs(sv.SumY / Math.Max(sv.Landed, 1)) < sv.TargetLengthM / 2 + 50;
        if (fc.Alive && fc.Generation == sv.FcGeneration)
        {
            long now = world.Time.Ms;
            Reported?.Invoke(new SalvoReport(sv.FiredMs, fc.Shooter, fc.Battery, sv.RangeM, sv.Landed, sv.SumX / Math.Max(sv.Landed, 1),
                sv.SumY / Math.Max(sv.Landed, 1), sv.AimErrM, sv.AimErrAcrossM, sv.CorrR, sv.Hits, straddle));
            fc.Hits += sv.Hits;
            if (sv.Hits > 0 && fc.FirstHitMs < 0)
                fc.FirstHitMs = now;
            if (straddle)
            {
                fc.Straddles++;
                if (fc.FirstStraddleMs < 0)
                    fc.FirstStraddleMs = now;
                log.Add(new CombatEntry(now, CombatEvent.Straddle, fc.Shooter, fc.Battery, fc.Target, sv.Landed));
            }
        }
        world.Schedule(new WorldTime(world.Time.Ms + settings.SpotDelayMs), EventTier.Fine, si, Id, SpotEvent, sv.Generation);
    }

    /// <summary>The spotter's judgement on a salvo that's down, if he sees the target: over, short or straddling, and its
    /// deflection, into the battery's corrections.</summary>
    void Spot(World world, int si)
    {
        ref var sv = ref salvos[si];
        ref var fc = ref fcs[sv.Fc];
        var store = ships.Store;
        int slot = fc.Alive && fc.Generation == sv.FcGeneration && fc.Target == sv.Target ? store.SlotOf(fc.Shooter) : -1;
        int tslot = store.SlotOf(sv.Target);
        if (slot >= 0 && tslot >= 0)
        {
            var ship = store[slot];
            var pic = world.Sightings.Of(ship.Side);
            int ci = pic?.IndexOf(sv.Target) ?? -1;
            bool sees = false;
            if (ci >= 0 && pic![ci].InSight)
            {
                var frame = ship.Frame;
                var target = store[tslot];
                for (int k = 0; k < fc.SensorCount && !sees; k++)
                {
                    ref readonly var s = ref sensors[sv.Fc * MaxSensors + k];
                    sees = Sees(ship.Handling.Class, s, frame.ToWorld(s.X, s.Y, 0), ship.HeadingDeg, target.Position, target.Handling.Class.Silhouette.TopM);
                }
            }
            if (sees)
            {
                var sign = sv.Hits > 0 || sv.Over > 0 && sv.Short > 0 ? FireControl.Sign.Straddle : sv.Over > 0 ? FireControl.Sign.Over : FireControl.Sign.Short;
                fc.CorrR = fc.Ladder.Apply(sign, sv.CorrR, sv.PatternM, settings.SpotLadderMinM);
                var r = stream.Derive(0x5B08u).Derive((ulong)si).Derive((ulong)sv.Generation);
                double defl = sv.SumY / Math.Max(sv.Landed, 1) + settings.SpotDeflectionMil * FireControl.Mil * sv.RangeM * r.NextGaussian();
                if (Math.Abs(defl) > 0.25 * sv.PatternM)
                    fc.CorrD = sv.CorrD - settings.SpotDeflectionGain * defl;
            }
        }
        FreeSalvo(si);
    }

    static double Mod(double d) => (d % 360 + 360) % 360;

    static double Wrap(double d) => d - 360 * Math.Round(d / 360);

    // ------------------------------------------------------------------ reading it

    /// <summary>An engaged battery's counts so far, for tools and tests: salvos, shells, hits, straddles, when it first
    /// hit and straddled (ms, -1 if not yet), and when its track started.</summary>
    public readonly record struct Tally(int Salvos, int Shells, int Hits, int Straddles, long FirstHitMs, long FirstStraddleMs, long TrackMs,
        BatteryState State);

    /// <summary>A ship's battery's tally, if it's engaged.</summary>
    public Tally? TallyOf(int shooter, int battery)
    {
        for (int i = 0; i < highFc; i++)
            if (fcs[i].Alive && fcs[i].Shooter == shooter && fcs[i].Battery == battery)
            {
                ref readonly var fc = ref fcs[i];
                return new Tally(fc.Salvos, fc.Shells, fc.Hits, fc.Straddles, fc.FirstHitMs, fc.FirstStraddleMs, fc.Tracking ? fc.TrackMs : -1, fc.State);
            }
        return null;
    }

    /// <summary>Engaged batteries now.</summary>
    public int EngagedCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < highFc; i++)
                if (fcs[i].Alive)
                    n++;
            return n;
        }
    }

    public void Hash(ref StateHash h)
    {
        if (crewsSet)
            for (int s = 0; s < crews.Length; s++)
                if (crews[s] != CrewQuality.Trained)
                {
                    h.Add(s);
                    h.Add((int)crews[s]);
                }
        if (!ever)
            return;   // a world that never engaged hashes as before there was gunnery
        for (int i = 0; i < highFc; i++)
        {
            ref readonly var fc = ref fcs[i];
            if (!fc.Alive)
                continue;
            h.Add(i);
            h.Add(fc.Generation);
            h.Add(fc.Shooter);
            h.Add(fc.Battery);
            h.Add(fc.Target);
            h.Add(fc.Track.Position.X);
            h.Add(fc.Track.Position.Y);
            h.Add(fc.Track.Velocity.X);
            h.Add(fc.Track.Ms);
            h.Add(fc.CorrR);
            h.Add(fc.CorrD);
            h.Add(fc.Ladder.Step);
            h.Add(fc.Salvos);
            h.Add(fc.Hits);
            h.Add(fc.PendCount);
        }
        for (int i = 0; i < highSalvo; i++)
            if (salvos[i].Alive)
            {
                h.Add(i);
                h.Add(salvos[i].Left);
                h.Add(salvos[i].Over);
            }
        ships.Store.Combat.Hash(ref h, ships.Store);
        log.Hash(ref h);
    }

    public void Fill(Snapshot snap)
    {
        var v = snap.Batteries;
        v.EnsureCapacity(highFc);
        int n = 0;
        var store = ships.Store;
        for (int i = 0; i < highFc; i++)
        {
            ref readonly var fc = ref fcs[i];
            int slot = fc.Alive ? store.SlotOf(fc.Shooter) : -1;
            if (slot < 0 || !snap.ShownSides[store[slot].Side])
                continue;
            (v.Shooter[n], v.Battery[n], v.Target[n], v.State[n]) = (fc.Shooter, fc.Battery, fc.Target, fc.State);
            (v.Salvos[n], v.Shells[n], v.Hits[n], v.Level[n], v.Local[n]) = (fc.Salvos, fc.Shells, fc.Hits, (byte)fc.Level, fc.Local);
            n++;
        }
        v.Count = n;
        log.Fill(snap.CombatLog, 64);
    }
}
