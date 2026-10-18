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
    SetDiscipline = 4,
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

/// <summary>Where an engaged battery is in its fire discipline (docs/battle/fire-control.md F6).</summary>
public enum FirePhase : byte
{
    /// <summary>The track settling; the guns load and lay, and wait.</summary>
    Opening,
    /// <summary>Finding the bracket: partial salvos, each fired when the last one's spot is in, so each carries something
    /// new.</summary>
    Ranging,
    /// <summary>Fire for effect, from the spotter's straddle: full salvos as fast as the guns load and lay, the spots
    /// still correcting; back to ranging when a run of them isn't straddling or the solution is disturbed.</summary>
    Effect,
}

/// <summary>The batteries' fire control and firing (docs/battle/fighting.md 4.2b; a port of
/// research/simulation/fire_control_ref.py <c>engage()</c> cut to its spine, one cause of a miss a stage):
/// <list type="bullet">
/// <item><b>Measure.</b> Every <see cref="SimSettings.FireControlMs"/> on a grid of world time, each engaged battery's
/// rangefinders (its fire-control units' instruments; without a director, a mount's layer by eye) take readings at their
/// optics' rate while they see the target: the range's error δ·R²/(B·M) (by eye ~20%), each instrument's own bias drawn
/// at the engagement's start, the bearing's in mil. A reading reaches the computer after its latency.</item>
/// <item><b>Track.</b> The computer's tracker (<see cref="FcTracker"/>, fire-control.md F2: a sailor's judgement, a rate
/// clock, a true course, a curved course or a turn), started from its own first reading and the eye's guess at the
/// target's course and speed (the synthetic method), never from the navy's picture.</item>
/// <item><b>Solve.</b> The target where it'll be after the time of flight, from the track (which lags by the latency),
/// the gun laid for it with its ship's motion in (<see cref="FireControl.Solve"/>), plus the engagement's residual bias
/// and the spotting corrections.</item>
/// <item><b>Lay, fire.</b> Mounts train and elevate as analytic anchors (<see cref="CombatStore"/>). The fire discipline
/// (fire-control.md F6, <see cref="FirePhase"/>): after the opening, a battery ranges with partial salvos, each going
/// when the last one's spot is in; on the spotter's straddle it fires for effect, a full salvo at the first instant the
/// bearing mounts are loaded and laid. Each salvo draws one common error (the computer's salvo-to-salvo error, times
/// battle's <see cref="SimSettings.GunneryCombat"/>; the director's laying error, or each gun's for a battery without
/// director firing), each shell its own dispersion from the gun's pattern; a ship swinging adds the computer's own-turn
/// kick.</item>
/// <item><b>Spot.</b> When a salvo's last shell is down, and <see cref="SimSettings.SpotDelayMs"/> on, the spotter (the
/// highest eye) judges it over, short or straddling (σ = k·R²/h) and sees its deflection; the range spots make a bracket
/// in correction space (<see cref="FireControl.Bracket"/>), each against the correction its salvo was fired with, and
/// the next salvo goes into it.</item>
/// </list>
/// Hits are counted and logged but do nothing yet (4.3a). Batteries fire only on the fine tick. World state: replays,
/// hashes. Registered fourth, so its id is always <see cref="Id"/>.</summary>
public sealed class GunnerySystem : IWorldSystem
{
    public const byte Id = 4;
    const ushort TickEvent = 1, SalvoEvent = 2, SpotEvent = 3;
    const int MaxSensors = 8, PendingCap = 16;

    /// <summary>A battery's fire control while it's engaged.</summary>
    struct Fc
    {
        public bool Alive;
        public int Generation;
        public int Shooter, Battery, Target, Level, Table;
        public bool Local;
        /// <summary>Engaged at a preset's fit (a bench's or a dev's), kept as it is in local control.</summary>
        public bool Preset;
        /// <summary>The preset it fires at under its directors, a label (it falls to local control's while they're out).</summary>
        public int DirectorLevel;
        /// <summary>Its fire control's fit now, and under its directors.</summary>
        public FireControlFit Fit, DirectorFit;
        /// <summary>The bearing (from ahead) and elevation the mounts were last ordered to, degrees; the last solution's
        /// elevation in the plane of fire and time of flight (where the next second's starts).</summary>
        public double OrderBearingDeg, OrderElevationDeg, ElevationDeg, TofS;
        public long StartMs, OpenMs, TrackMs;
        public bool Tracking, SalvoPending;
        public FcTracker Tracker;
        /// <summary>When a judgement looks at the target again.</summary>
        public long JudgeMs;
        public double BiasR, BiasD, CorrR, CorrD, SpotHeightM;
        /// <summary>Its fire discipline (F6): the phase, the spots' bracket, and the computer's doubt of the range rate
        /// along the line of fire (m/s: the bracket widens at it).</summary>
        public FirePhase Phase;
        /// <summary>Whether a sensor of it saw the target on its last second (its layers lay their line on her then), and
        /// whether she's inside its point-blank range (laid by sight at her present range, no ranging), with the present
        /// range's error by sight its layers will lay at for the next salvo (m).</summary>
        public bool Sees, PointBlank, WasPointBlank;
        /// <summary>Point blank: the error of the layers' sight setting (in sigmas, drawn as she comes into the band), and
        /// the correction they've walked onto it from their own falls, a share of her range (the errors it takes off are
        /// shares of it: a correction walked in at 2 km is a quarter of the metres at 500 m).</summary>
        public double SightZ, PbCorrR;
        public FireControl.Bracket Bracket;
        public double DriftMs;
        /// <summary>How far the computer's solution has moved in range by its readings since the engagement began, m (the
        /// bracket moves the other way: <see cref="Rebase"/>).</summary>
        public double Shift;
        /// <summary>The computer's doubt of the target's position along the line of fire (m), and the range to its aim
        /// (m), for the first step.</summary>
        public double PosSigmaM, RangeM;
        /// <summary>Which way the last spot moved the aim (-1, 0, +1): two the same way correct the rate too.</summary>
        public int LastMove;
        /// <summary>Its salvos in the air or down and not yet spotted; while ranging the next waits for them, or until
        /// <see cref="WaitMs"/> (a spot that never came).</summary>
        public int InFlight;
        public long WaitMs;
        /// <summary>In fire for effect, the spots in a row that weren't straddles, and of them overs and shorts.</summary>
        public int Misses, MissOver, MissShort;
        /// <summary>Which mount the next partial salvo starts from (the groups alternate), and the tracker's turns seen.</summary>
        public int Group, Turns;
        /// <summary>A ladder's salvos fired in its group so far (the discipline's ladder: the group goes at once, stepped).</summary>
        public int Rung;
        /// <summary>Firing for effect in halves, which half goes next (0 the larger).</summary>
        public int Half;
        public bool OwnTurning;
        public int RangingSalvos, RangingShells;
        public long EffectMs, LastSalvoMs, SettleMs;
        public int Salvos, Shells, Hits, Straddles, Arm, SensorCount, PendHead, PendCount;
        /// <summary>The shooter's condition's version its instruments were picked at (damage: a director lost).</summary>
        public int Condition;
        /// <summary>The fire-control units it reads from (a bit a unit), and whether a working one of its own was held by a
        /// battery on another target when they were picked (so it picks again when one is let go).</summary>
        public ulong Units;
        public bool Short, Repick;
        public long FirstHitMs, FirstStraddleMs;
        public BatteryState State;
    }

    /// <summary>A rangefinder or an eye a battery reads from: the class's eye (-1: a mount's layer), where it stands and
    /// on what, its height and base, its bearing's error (mil, by its position), its next reading and its own bias (in
    /// sigmas).</summary>
    struct Sensor
    {
        public int Eye;
        public InstrumentSupport Support;
        public double X, Y, HeightM, BaseM, BearingMil, Bias;
        public long NextMs;
    }

    /// <summary>A sensor at one of the class's eyes.</summary>
    static Sensor SensorAt(ShipClass cls, int eye)
    {
        ref readonly var e = ref cls.Eyes[eye];
        return new Sensor
        {
            Eye = eye, Support = e.Support, X = e.X, Y = e.Y, HeightM = e.HeightM, BaseM = e.RangefinderM, BearingMil = SightModel.BearingSigmaRad(e) / FireControl.Mil,
        };
    }

    /// <summary>Whether one of a battery's first n sensors is at an eye.</summary>
    bool Taken(int f, int n, int eye)
    {
        for (int k = 0; k < n; k++)
            if (sensors[f * MaxSensors + k].Eye == eye)
                return true;
        return false;
    }

    /// <summary>A salvo in the air, for its spot: whose, at whom, the corrections it was fired with, its pattern and line
    /// of fire, and its fall so far.</summary>
    struct Salvo
    {
        public bool Alive;
        public int Generation, Fc, FcGeneration, Target, Shells, Left, Landed, Over, Short, Hits;
        public double CorrR, CorrD, PatternM, RangeM, SpotSigmaM, Common, SumY, MinX, MaxX, TargetLengthM, SumX, AimErrM, AimErrAcrossM;
        public long FiredMs;
        public FirePhase Phase;
        /// <summary>Its battery's <see cref="Fc.Shift"/> when it was fired, and whether it was laid point blank (its fall
        /// says nothing to the bracket: its range was the layers' own).</summary>
        public double Shift, TrueRangeM, PbCorrR;
        public bool PointBlank;
        /// <summary>Its ladder's step off the bracket's aim, m (in its correction).</summary>
        public double LadderM;
        public Vec3d U, W;
    }

    /// <summary>A salvo's fall, for tools (not world state): when it was fired and by whom, its range and shells, where
    /// its shells fell on average against the target (along the line of fire and to its right, m), how far the aim
    /// point was from where the target truly went (the track's error, as the target's course and speed carry it on),
    /// the range correction it was fired with (its ladder's step in it; laid point blank, what its layers had walked onto
    /// their sight), its hits, whether it straddled, the phase it was fired in, her true range, and whether point blank.</summary>
    public readonly record struct SalvoReport(long FiredMs, int Shooter, int Battery, double RangeM, int Shells, double MeanX, double MeanY, double AimErrM,
        double AimErrAcrossM, double CorrR, int Hits, bool Straddle, FirePhase Phase, double LadderM, double TrueRangeM, bool PointBlank);

    /// <summary>A salvo's spot, for tools: the salvo (by when it was fired), whether the spotter saw its fall and what he
    /// judged it, the bracket after it (m; infinite: open) and the correction the next salvo goes with, and the phase
    /// after it.</summary>
    public readonly record struct SpotReport(long FiredMs, int Shooter, int Battery, bool Seen, FireControl.Sign Sign, double Lo, double Hi, double Next,
        FirePhase Phase);

    /// <summary>Told of each salvo once it's down (tools and benches; the world never reads it).</summary>
    public Action<SalvoReport>? Reported { get; set; }

    /// <summary>Told of each salvo's spot (tools and benches).</summary>
    public Action<SpotReport>? Spotted { get; set; }

    readonly SimSettings settings;
    readonly ShipSystem ships;
    readonly ShellSystem shells;
    readonly CombatLog log;
    readonly RandomStream stream;
    // the engagements' pool, two a ship the store is made for (a ship's main and secondary batteries engaged at once), so a
    // fight's new peak doesn't grow it within a step
    Fc[] fcs;
    Sensor[] sensors;
    FcReading[] pending;
    readonly Stack<int> freeFc;
    int highFc;
    Salvo[] salvos;
    readonly Stack<int> freeSalvo;
    int highSalvo;
    bool ticking, ever;
    readonly CrewQuality[] crews = new CrewQuality[256];
    bool crewsSet;
    // each side's fire discipline, an index of the settings' FireDisciplines (fire-control.md F6, decision 3)
    readonly byte[] disciplines = new byte[256];
    bool disciplinesSet;
    bool[] firing = new bool[64];
    int[] fireLoad = new int[64];

    internal GunnerySystem(SimSettings settings, ShipSystem ships, ShellSystem shells, CombatLog log, ulong seed)
    {
        (this.settings, this.ships, this.shells, this.log) = (settings, ships, shells, log);
        stream = RandomStream.Named(seed, "gunnery");
        salvos = new Salvo[Math.Max(settings.SalvoCapacity, 16)];
        freeSalvo = new Stack<int>(salvos.Length);   // as big as the pool, so freeing never grows it
        int engagements = Math.Max(16, 2 * settings.ShipCapacity);
        (fcs, sensors, pending, freeFc) = (new Fc[engagements], new Sensor[engagements * MaxSensors], new FcReading[engagements * PendingCap],
            new Stack<int>(engagements));
        Array.Fill(crews, CrewQuality.Trained);
    }

    // ------------------------------------------------------------------ commands

    /// <summary>A ship's battery (or all of them: -1) engages a ship (an entity). A level of -1 is the battery's own fit; a
    /// dev or a bench may try another preset's (0-8).</summary>
    public static Command Engage(int shooter, int battery, int target, int level = -1) =>
        new(Id, (ushort)GunneryCommand.Engage, shooter, A: target, B: battery, X: level);

    /// <summary>A ship's battery (or all: -1) ceases fire.</summary>
    public static Command Cease(int shooter, int battery = -1) => new(Id, (ushort)GunneryCommand.Cease, shooter, B: battery);

    /// <summary>A side's gun crews (the scenario's to give; trained by default).</summary>
    public static Command SetCrew(int side, CrewQuality quality) => new(Id, (ushort)GunneryCommand.SetCrew, side, A: (long)quality);

    /// <summary>A side's fire discipline, an index of <see cref="SimSettings.FireDisciplines"/> (the scenario's or the
    /// admiral's to give; the first by default).</summary>
    public static Command SetDiscipline(int side, int discipline) => new(Id, (ushort)GunneryCommand.SetDiscipline, side, A: discipline);

    public CrewQuality Crew(int side) => crews[side & 255];

    public FireDiscipline Discipline(int side) => settings.FireDisciplines[disciplines[side & 255]];

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
            case GunneryCommand.SetDiscipline:
                if (c.Entity is < 0 or > 255 || c.A < 0 || c.A >= settings.FireDisciplines.Length)
                    throw new ArgumentException($"no fire discipline {c.A} for side {c.Entity}");
                disciplines[c.Entity] = (byte)c.A;
                disciplinesSet = true;
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
            int lv = Math.Clamp(level >= 0 ? level : row.Local ? 0 : (int)row.Level, 0, FireControlPresets.Count - 1);
            var fit = level >= 0 ? FireControlPresets.At(lv) : row.Fit;   // a battery without directors: local control, its own sights
            fc = new Fc
            {
                Alive = true, Generation = gen, Shooter = shooter, Battery = b, Target = target, StartMs = now, Level = lv, Local = row.Local, Preset = level >= 0,
                FirstHitMs = -1, FirstStraddleMs = -1, EffectMs = -1, DirectorLevel = lv, Fit = fit, DirectorFit = fit,
                Table = shells.TableOf(store.Combat.Loadout(slot), b), ElevationDeg = double.NaN,
            };
            fc.Bracket = FireControl.Bracket.Open(fit.Computer.FirstStepM);
            var r = stream.Derive((ulong)f).Derive((ulong)gen);
            fc.BiasR = r.NextGaussian();
            fc.BiasD = r.NextGaussian();
            AddSensors(ref fc, f, cls, row, store.Combat.Condition(slot), now, ref r);
            store.Combat.Engage(slot, b, target);
        }
        ever = true;
        if (store[slot].Tier != ClockTier.Fine && store[slot].OnRails)
            world.Ships.Derail(world, shooter);
        Tick(world, schedule: true);
    }

    /// <summary>A battery's rangefinders: the first working instrument of each of its units whose director works and
    /// that no battery on another target holds (one target a director: fire-control.md F0), longest base first (as many
    /// as <see cref="MaxSensors"/>); without one, the highest of its working mounts' layers by eye, at local control.</summary>
    void AddSensors(ref Fc fc, int f, ShipClass cls, in BatteryRow row, ShipCondition cond, long now, ref RandomStream r)
    {
        int n = 0;
        ulong had = fc.Units;
        (fc.Condition, fc.Units, fc.Short, fc.Repick) = (cond.Version, 0, false, false);
        if (!row.Local)
            foreach (int u in cls.Links(row.Units))
            {
                ref readonly var unit = ref cls.FcUnits[u];
                if (unit.Director >= 0 && !cond.DirectorWorks[unit.Director] || n == MaxSensors)
                    continue;
                if (HeldElsewhere(f, u))
                {
                    fc.Short = true;
                    continue;
                }
                foreach (int i in cls.Links(unit.Instruments))
                {
                    ref readonly var ins = ref cls.Instruments[i];
                    if (!cond.EyeWorks[ins.Eye] || n == MaxSensors)
                        continue;
                    sensors[f * MaxSensors + n++] = SensorAt(cls, ins.Eye);
                    fc.Units |= 1UL << u;
                }
            }
        if ((had & ~fc.Units) != 0)
            LetGo(f);
        if (n > 0)
        {
            // every working gunhouse's rangefinder reports to the computer too (fire-control.md F5: the transmitting
            // station plots every instrument's cuts), each through its turret's arcs, longest base first
            while (n < MaxSensors)
            {
                int pick = -1;
                foreach (int m in cls.Links(row.Mounts))
                {
                    int eye = cls.Mounts[m].RangefinderEye;
                    if (eye < 0 || !cond.EyeWorks[eye] || Taken(f, n, eye))
                        continue;
                    if (pick < 0 || cls.Eyes[eye].RangefinderM > cls.Eyes[pick].RangefinderM)
                        pick = eye;
                }
                if (pick < 0)
                    break;
                sensors[f * MaxSensors + n++] = SensorAt(cls, pick);
            }
        }
        if (n == 0)
        {
            // local control (fire-control.md F4b): the working mounts' rangefinders, each in its gunhouse's hood and
            // seeing only through its turret's arcs, highest first (one sensor slot kept); then the highest working
            // mount's layer, by eye, all round (any mount's, when none works)
            bool rangefinders = false;
            while (n < MaxSensors - 1)
            {
                int pick = -1;
                double top = double.MinValue;
                foreach (int m in cls.Links(row.Mounts))
                {
                    int eye = cls.Mounts[m].RangefinderEye;
                    if (eye < 0 || !cond.Lays(m, local: true) || !cond.EyeWorks[eye] || cls.Eyes[eye].HeightM <= top || Taken(f, n, eye))
                        continue;
                    (pick, top) = (m, cls.Eyes[eye].HeightM);
                }
                if (pick < 0)
                    break;
                sensors[f * MaxSensors + n++] = SensorAt(cls, cls.Mounts[pick].RangefinderEye);
                rangefinders = true;
            }
            double best = double.MinValue;
            bool works = false;
            var s = new Sensor { Eye = -1, BearingMil = settings.EyeBearingMil };
            foreach (int m in cls.Links(row.Mounts))
            {
                ref readonly var comp = ref cls.Components[cls.Mounts[m].Component];
                double h = comp.Bounds.Top - cls.Vertical.Waterline;
                bool w = cond.Lays(m, local: true);   // its layer can't see with its sights gone (E4)
                if (w && !works || w == works && h > best)
                    (best, works, s.X, s.Y, s.HeightM) = (h, w, cls.Mounts[m].X, cls.Mounts[m].Y, h);
            }
            sensors[f * MaxSensors + n++] = s;
            // its guns' own sights (not the eyeball's open sights: a dreadnought's layers had telescopes)
            var local = rangefinders ? FireControlPresets.Local with { Optics = row.TurretOptics, Mount = row.Sights } : FireControlPresets.Local with { Mount = row.Sights };
            if (!fc.Local)
                (fc.Level, fc.Fit) = (0, local);   // its directors gone: each mount for itself, by its rangefinder or by eye
            else if (!fc.Preset)
                fc.Fit = local;   // a battery under local control (not a bench's preset): its turret's rangefinder now, or none
            fc.Local = true;
        }
        else
        {
            // under its directors, or only control positions' rangefinders (fire-control.md F4b): then the guns lay by their
            // own sights on the station's solution, no director firing
            bool directed = false;
            foreach (int u in cls.Links(row.Units))
                directed |= (fc.Units & 1UL << u) != 0 && cls.FcUnits[u].Director >= 0;
            var fit = directed ? fc.DirectorFit : fc.DirectorFit with { Mount = row.Sights };
            if (fc.Local || fc.Fit != fit)
                (fc.Local, fc.Level, fc.Fit) = (false, fc.DirectorLevel, fit);   // a director back (E4: a timed outage over): under it again
        }
        fc.SensorCount = n;
        fc.SpotHeightM = 0;
        var optics = fc.Fit.Optics;
        for (int k = 0; k < n; k++)
        {
            ref var s = ref sensors[f * MaxSensors + k];
            s.Bias = r.NextGaussian();
            s.NextMs = now + (long)(r.NextDouble() * 60_000 / Math.Max(Rate(optics, s), 1e-6));
            fc.SpotHeightM = Math.Max(fc.SpotHeightM, s.HeightM);
        }
    }

    /// <summary>A sensor's readings a minute: an eye one, a rangefinder its optics', fewer past ×30 where the narrow field
    /// is slower to pick the target up (designer §3.1: ×√(30/M) [INFERRED]).</summary>
    static double Rate(in OpticsRow o, in Sensor s) => Ranges(o, s) ? o.ReadingsPerMin * Math.Sqrt(Math.Min(1, 30 / o.Magnification)) : 1;

    /// <summary>Whether a sensor takes ranges with its rangefinder: it has one, and the fit's optics can read it (the
    /// eyeball's can't: a director's rangefinder under it ranges by eye).</summary>
    static bool Ranges(in OpticsRow o, in Sensor s) => s.BaseM > 0 && o.Reads;

    /// <summary>Whether a battery of the same ship on another target holds a unit.</summary>
    bool HeldElsewhere(int f, int unit)
    {
        ref readonly var fc = ref fcs[f];
        for (int j = 0; j < highFc; j++)
            if (j != f && fcs[j].Alive && fcs[j].Shooter == fc.Shooter && fcs[j].Target != fc.Target && (fcs[j].Units & 1UL << unit) != 0)
                return true;
        return false;
    }

    /// <summary>A battery let go of units: its ship's batteries that went short pick theirs again on their next second.</summary>
    void LetGo(int f)
    {
        for (int j = 0; j < highFc; j++)
            if (j != f && fcs[j].Alive && fcs[j].Shooter == fcs[f].Shooter && fcs[j].Short)
                fcs[j].Repick = true;
    }

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
        if (fc.Units != 0)
            LetGo(f);
        fc.Units = 0;
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
        if (cond.Version != fc.Condition || fc.Repick)
        {
            // damage: its instruments picked again from what still works; or a unit it was short of let go
            var rs = cond.Version != fc.Condition
                ? stream.Derive((ulong)f).Derive((ulong)fc.Generation).Derive(0xDA3u).Derive((ulong)cond.Version)
                : stream.Derive((ulong)f).Derive((ulong)fc.Generation).Derive(0xDA4u).Derive((ulong)now);
            AddSensors(ref fc, f, cls, cls.Batteries[fc.Battery], cond, now, ref rs);
        }
        var computer = fc.Fit.Computer;
        var optics = fc.Fit.Optics;
        var crew = settings.GunneryCrews[(int)crews[ship.Side]];
        var pic = world.Sightings.Of(ship.Side);
        int ci = pic?.IndexOf(fc.Target) ?? -1;
        bool inSight = ci >= 0 && pic![ci].InSight;
        if (!fc.Tracking && !inSight)
        {
            fc.State = BatteryState.NoContact;
            return;
        }
        var ownV = ship.Forward * ship.Speed;

        // measure: each sensor that's due and makes the target out through its optics
        var frame = ship.Frame;
        var tpos = target.Position;
        double combat = settings.GunneryCombat;
        Air air = default;
        bool sees = false, aired = false;
        for (int k = 0; k < fc.SensorCount; k++)
        {
            ref var s = ref sensors[f * MaxSensors + k];
            bool due = now >= s.NextMs;
            if (!due && (sees || !inSight))
                continue;   // one that sees is enough to fire on; the margins are looked at for readings
            if (!aired)
                (air, aired) = (AirAt(world, ship.Position), true);
            var at = frame.ToWorld(s.X, s.Y, 0);
            double margin = inSight ? MarginOf(cls, s, optics, at, ship.HeadingDeg, target, air) : 0;
            bool seen = margin >= 1;
            sees |= seen;
            if (!due)
                continue;
            var r = stream.Derive((ulong)f).Derive((ulong)fc.Generation).Derive((ulong)now).Derive((ulong)k);
            s.NextMs = now + (long)(60_000 / Rate(optics, s) * r.NextDouble(0.7, 1.3));
            if (!seen)
                continue;
            double range = Earth.Distance(at, tpos), brg = Earth.Bearing(at, tpos), sr, bias, sb;
            if (Ranges(optics, s))
            {
                // its unit of error, worse as the air takes the target's contrast toward the eye's threshold (F5c)
                double kAir = SightModel.RangingFactor(margin, optics, settings);
                double unit = kAir * FireControl.RangefinderUnitM(s.BaseM, optics, FireControl.Magnification(optics, s.Support, s.HeightM), range);
                (sr, bias, sb) = (crew.Operator * combat * unit, s.Bias * settings.RangefinderBiasUnits * unit, s.BearingMil * Math.Sqrt(kAir) * FireControl.Mil);
            }
            else
                (sr, bias, sb) = (settings.EyeRangeShare * range * crew.Operator * combat, s.Bias * settings.EyeRangeBiasShare * range,
                    s.BearingMil * FireControl.Mil);
            double zr = Math.Max(range + bias + sr * r.NextGaussian(), 100), zb = brg + double.RadiansToDegrees(sb * r.NextGaussian());
            var (z, r11, r12, r22) = Track.Reading(at, zr, zb, sr, sb);
            if (fc.PendCount == PendingCap)
                PopReading(ref fc, f, target, now, ship.Position, ownV);   // the computer's queue is full: the oldest goes in now
            int at2 = f * PendingCap + (fc.PendHead + fc.PendCount++) % PendingCap;
            // the computer takes every instrument's cut from one reference point, the ship's (the tables' parallax
            // correction: a director forward and one aft see the target on bearings tens of mils apart close in)
            pending[at2] = new FcReading
            {
                Ms = now, ArriveMs = now + (long)(computer.LatencyS * 1000), From = ship.Position, OwnVelocity = ownV, Z = z,
                RangeM = Earth.Distance(ship.Position, z), BearingDeg = Earth.Bearing(ship.Position, z), SigmaRangeM = sr, SigmaBearingRad = sb, R11 = r11,
                R12 = r12, R22 = r22,
            };
        }

        // the computer: readings that have come through
        while (fc.PendCount > 0 && pending[f * PendingCap + fc.PendHead].ArriveMs <= now)
            PopReading(ref fc, f, target, now, ship.Position, ownV);
        if (!fc.Tracking)
        {
            fc.State = BatteryState.Tracking;   // its first reading still on its way
            return;
        }
        if (computer.Tracker == TrackerKind.Judgement && sees && now >= fc.JudgeMs)
        {
            // a sailor looks at her again: a fresh guess at her course and speed
            var rg = stream.Derive((ulong)f).Derive((ulong)fc.Generation).Derive(0xE7Fu).Derive((ulong)now);
            double before = SolutionRangeM(fc, now, ship.Position, ownV);
            fc.Tracker.Rejudge(Guess(computer, target.Position, target.Forward * target.Speed, ref rg));
            Rebase(ref fc, SolutionRangeM(fc, now, ship.Position, ownV) - before);
            fc.JudgeMs = now + (long)(computer.RejudgeS * 1000);
        }

        // solve, and order the mounts
        ref readonly var row = ref cls.Batteries[fc.Battery];
        var mounts = cls.Links(row.Mounts);
        var table = shells.Ballistics[fc.Table];
        var muzzle = Muzzle(cls, frame, mounts[0]);
        fc.Sees = sees;
        var aim = Aim(ref fc, f, table, muzzle, now, ship.Position, ownV, target, crew);
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
            firing[j] = inTraverse && cond.Lays(mounts[j], fc.Local) && CombatStore.InArcs(cls, mr, bearing) &&
                        store.Combat.Mount(slot, mounts[j]).Rounds > 0 && elevation <= mr.MaxElevationDeg;
            bears |= firing[j];
        }
        fc.State = !laid ? BatteryState.OutOfRange : !bears ? BatteryState.NotBearing : now < fc.OpenMs ? BatteryState.Tracking : BatteryState.Firing;

        // fire discipline (F6): the bracket widens at the computer's doubt of the range rate along the line of fire; own
        // ship's turn (any for a machine that takes it as the target's, a hard one for the rest) or the target seen to
        // turn sends a battery firing for effect back to ranging, its bracket opened to the first step
        var along = Earth.Forward(aim, Earth.Bearing(ship.Position, aim));
        (fc.DriftMs, fc.PosSigmaM, fc.RangeM) = (fc.Tracker.RateSigma(computer, along), fc.Tracker.PositionSigma(along), Earth.Distance(ship.Position, aim));
        double yaw = Math.Abs(double.RadiansToDegrees(ship.YawRate));
        bool plain = !computer.HelmFree && computer.Tracker is TrackerKind.RateClock or TrackerKind.Judgement;
        bool turning = yaw > (plain ? settings.OwnTurnYawDegS : settings.HardTurnYawDegS);
        if (turning && !fc.OwnTurning || fc.Tracker.Turns != fc.Turns)
            Disturbed(ref fc, now);
        (fc.OwnTurning, fc.Turns) = (turning, fc.Tracker.Turns);
        if (fc.State == BatteryState.Firing && fc.Phase == FirePhase.Opening)
            (fc.Phase, fc.Bracket.Step) = (FirePhase.Ranging, FirstStep(fc));

        // time the salvo: ranging, when the spots it's due are in (the discipline's salvos in flight: a ladder's group at
        // once, else rolling) and enough guns are loaded and laid; firing for effect, the first instant the bearing mounts
        // are (or a half of them, by the discipline)
        var dis = settings.FireDisciplines[disciplines[ship.Side]];
        bool independent = Independent(dis, cls, mounts, fc.RangeM);
        if (fc.PointBlank && fc.Phase == FirePhase.Ranging)
            fc.Phase = FirePhase.Effect;   // point blank: no ranging, fire for effect
        bool ranging = fc.Phase == FirePhase.Ranging && !independent, waiting = fc.InFlight > 0 && now < fc.WaitMs;
        if (ranging && dis.LadderStepM > 0 && fc.Rung >= dis.InFlight && !waiting)
            fc.Rung = 0;   // the ladder's falls all spotted: the next group
        if (fc.State != BatteryState.Firing || fc.SalvoPending || !sees ||
            ranging && (dis.LadderStepM > 0 ? fc.Rung >= dis.InFlight : fc.InFlight >= dis.InFlight && waiting))
            return;
        long when = now;
        int bearingGuns = 0, bearingMounts = 0;
        for (int j = 0; j < mounts.Length; j++)
            (bearingGuns, bearingMounts) = firing[j] ? (bearingGuns + cond.Guns(mounts[j]), bearingMounts + 1) : (bearingGuns, bearingMounts);
        var (need, quota) = independent ? (int.MaxValue, 1) : Partial(dis, fc.Phase, bearingGuns, bearingMounts, fc.Half);
        if (!independent && quota < int.MaxValue && fc.Phase == FirePhase.Effect && fc.Salvos > 0)
            when = Math.Max(when, fc.LastSalvoMs + (long)(500 * cls.Mounts[mounts[0]].ReloadS));   // the halves alternate
        if (!independent)
            when = Math.Max(when, fc.SettleMs);   // the layers on again after a correction (firing independently, each his own)
        for (int k = 0, got = 0, taken = 0; k < mounts.Length && got < need && taken < quota; k++)
        {
            int j = (fc.Group + k) % mounts.Length;
            if (!firing[j])
                continue;
            ref readonly var ms = ref store.Combat.Mount(slot, mounts[j]);
            when = Math.Max(when, Math.Max(ms.LoadedMs, CombatStore.LaidBy(ms, cls.Mounts[mounts[j]])));
            (got, taken) = (got + cond.Guns(mounts[j]), taken + 1);
        }
        if (when < now + settings.FireControlMs)
        {
            fc.SalvoPending = true;
            fc.Arm++;
            world.Schedule(new WorldTime(when), EventTier.Fine, f, Id, SalvoEvent, fc.Generation, fc.Arm);
        }
    }

    /// <summary>The air at a place now: the visibility, the sun's elevation, and its bearing when it's low enough to
    /// silhouette a ship against the horizon (else NaN), as the sightings read them.</summary>
    readonly record struct Air(double VisibilityM, double SunElevationDeg, double SunBearingDeg);

    Air AirAt(World world, Vec3d at)
    {
        double el = Light.SunElevationDeg(at, world.Time);
        bool low = el >= settings.SightSilhouetteLowDeg && el <= settings.SightSilhouetteHighDeg;
        return new Air(world.Weather.At(at, world.Time).VisibilityM, el, low ? Light.SunBearingDeg(at, world.Time) : double.NaN);
    }

    /// <summary>A battery's sensor's margin on the target (fire-control.md F5c: the sighting model's, through its optics'
    /// magnification, or a layer's or an eye's binoculars): 0 when its eye doesn't bear (a blind sector) or the target is
    /// under the horizon; under 1 it can't make the target out, so takes no reading and doesn't see the fall.</summary>
    double MarginOf(ShipClass cls, in Sensor s, in OpticsRow optics, Vec3d at, double headingDeg, in ShipState target, in Air air)
    {
        var tc = target.Handling.Class;
        var tp = target.Position;
        double d = Earth.Distance(at, tp);
        if (d > SightModel.HorizonM(s.HeightM, settings) + SightModel.HorizonM(tc.Silhouette.TopM, settings))
            return 0;
        double brg = Earth.Bearing(at, tp);
        if (s.Eye >= 0 && !cls.Eyes[s.Eye].Sees(brg - headingDeg))
            return 0;
        bool sil = !double.IsNaN(air.SunBearingDeg) && Math.Abs(Wrap(brg - air.SunBearingDeg)) <= settings.SightSilhouetteArcDeg;
        var line = new SightLine(d, Earth.Bearing(tp, at) - target.HeadingDeg, air.VisibilityM, air.SunElevationDeg, sil);
        return SightModel.Margin(s.HeightM, Ranges(optics, s) ? optics.Magnification : settings.SightLookoutMagnification, tc, line, settings);
    }

    /// <summary>The oldest reading into the computer: the first starts the track, from the eye's guess at the target's
    /// course and speed.</summary>
    void PopReading(ref Fc fc, int f, in ShipState target, long now, Vec3d ownP, Vec3d ownV)
    {
        ref readonly var r = ref pending[f * PendingCap + fc.PendHead];
        var computer = fc.Fit.Computer;
        if (!fc.Tracking)
        {
            var rg = stream.Derive((ulong)f).Derive((ulong)fc.Generation).Derive(0xE7Eu);
            var truth = target.Forward * target.Speed;
            fc.Tracker = FcTracker.Start(computer, r, Guess(computer, target.Position, truth, ref rg), FcTracker.GuessSigma(computer, target.Speed));
            (fc.Tracking, fc.TrackMs, fc.OpenMs) = (true, now, now + (long)(settings.GunneryOpenS * 1000));
            fc.JudgeMs = now + (long)(computer.RejudgeS * 1000);
        }
        else
        {
            double before = SolutionRangeM(fc, now, ownP, ownV);
            fc.Tracker.Fix(computer, r);
            Rebase(ref fc, SolutionRangeM(fc, now, ownP, ownV) - before);
        }
        fc.PendHead = (fc.PendHead + 1) % PendingCap;
        fc.PendCount--;
    }

    /// <summary>The range the computer's solution puts the target at when a shell fired now would fall, m.</summary>
    double SolutionRangeM(in Fc fc, long now, Vec3d ownP, Vec3d ownV)
    {
        fc.Tracker.At(fc.Fit.Computer, now + (long)(fc.TofS * 1000), now, ownP, ownV, out var p, out _);
        return Earth.Distance(ownP, p);
    }

    /// <summary>The computer's solution moved by a reading (or a judgement's new look) by dr in range: the spots' bracket
    /// and the correction in force move the other way, so they keep saying where the target is (they're corrections to
    /// the solution: a solution that jumps 900 m on an eye's range would otherwise carry the old corrections with it, and
    /// put the salvo behind the ship), and the salvos in the air are spotted against what their correction now means.</summary>
    static void Rebase(ref Fc fc, double dr)
    {
        if (!(Math.Abs(dr) > 1e-6))
            return;
        fc.Bracket.Shift(-dr);
        fc.CorrR -= dr;
        fc.Shift += dr;
    }

    /// <summary>The eye's guess at a target's velocity: its speed and course, each off by the computer's prior (1 sigma).</summary>
    static Vec3d Guess(in ComputerRow c, Vec3d position, Vec3d velocity, ref RandomStream r)
    {
        double speed = velocity.Length, course = speed > 1e-6 ? Earth.Heading(position, velocity) : 0;
        double ds = r.NextGaussian() * c.PriorKn * Handling.MsPerKnot, dc = r.NextGaussian() * c.PriorDeg;
        return Earth.Forward(position, course + dc) * Math.Max(speed + ds, 0);
    }

    /// <summary>Where a battery's guns are laid (the short-range bug hunt, 2026-10-18; fire-control.md F7's point blank
    /// taken in): the line is the layers' own, laid on the target as they see her now, with the computer's lead over the
    /// time of flight on it (its estimate of her velocity: speed across is what it gives the sights); the range is the
    /// computer's, which the spots correct. Point blank, where the danger space of her side covers twice the error of her
    /// present range by sight (the battery's first instrument, or the layer's eye, with the computer's residual ballistic
    /// bias: what spotting would take off), the range is that present range,
    /// laid on the middle of her side, and there's no ranging (fire-control-research/emergency-eyeball.md §6: "a hit is
    /// essentially guaranteed if the line is right"). A battery that sees nothing lays where its computer says.</summary>
    Vec3d Aim(ref Fc fc, int f, BallisticTable table, Vec3d muzzle, long now, Vec3d ownP, Vec3d ownV, in ShipState target, in GunneryCrew crew)
    {
        var c = Predict(ref fc, table, muzzle, now, ownP, ownV);
        fc.PointBlank = false;
        if (!fc.Sees || double.IsNaN(fc.ElevationDeg))
            return c;
        fc.Tracker.At(fc.Fit.Computer, now, now, ownP, ownV, out _, out var v);
        var lead = (target.Position + v * fc.TofS).Normalized() * Earth.Radius;
        double range = Earth.Distance(ownP, c), present = Earth.Distance(ownP, lead);
        var tc = target.Handling.Class;
        double side = Math.Max(tc.Hull.DeckTop(0) - tc.Vertical.Waterline, 1);
        // her danger space at her present range (not the computer's: a clock run past her closest approach has her short)
        double h0 = muzzle.Length - Earth.Radius, e = table.ElevationFor(present, h0, 1, fc.ElevationDeg);
        if (double.IsNaN(e))
            return c;
        double fall = table.Fall(e, h0).FallDeg;
        double danger = side / Math.Tan(double.DegreesToRadians(Math.Max(fall, 0.05)));
        // the error of laying at her present range unspotted: the sight's, and the computer's residual ballistic bias (the
        // propellant, the air, the gun's wear; small at short range: a share of the range)
        var (sigma, bias) = PresentRangeError(fc, f, crew, present);
        double resid = fc.Fit.Computer.ResidPct / 100 * present;
        // point blank where the danger space covers the error, or where the layers read their own falls finely enough to walk
        // them onto her (their spotting error from their guns' height under a quarter of it: the user, 2026-10-18)
        double own = settings.SpotOpticK * settings.GunneryCombat * present * present / Math.Max(h0, 1);
        bool pb = danger >= 2 * Math.Sqrt(sigma * sigma + bias * bias + resid * resid) || own <= 0.25 * danger;
        if (pb && !fc.WasPointBlank)
        {
            // into the band: the sight set to her present range as judged then, nothing walked onto it yet
            var rz = stream.Derive((ulong)f).Derive((ulong)fc.Generation).Derive(0x5B0Au).Derive((ulong)now);
            (fc.SightZ, fc.PbCorrR) = (rz.NextGaussian(), 0);
        }
        fc.WasPointBlank = pb;
        if (pb)
            (fc.PointBlank, range) = (true, Math.Max(present * (1 + fc.PbCorrR) + 0.5 * danger + bias + sigma * fc.SightZ, 50));
        var p = ownP;
        var fw = Earth.Forward(p, Earth.Bearing(ownP, lead));
        Earth.Advance(ref p, ref fw, range);
        return p;
    }

    /// <summary>The error of a battery's present range by sight (point blank): its first instrument's, or its layer's eye's
    /// (1 sigma, m), and that instrument's own bias, m.</summary>
    (double Sigma, double Bias) PresentRangeError(in Fc fc, int f, in GunneryCrew crew, double rangeM)
    {
        if (fc.SensorCount == 0)
            return (settings.EyeRangeShare * rangeM, 0);
        ref readonly var s = ref sensors[f * MaxSensors];
        var o = fc.Fit.Optics;
        if (!Ranges(o, s))
            return (settings.EyeRangeShare * rangeM * crew.Operator * settings.GunneryCombat, s.Bias * settings.EyeRangeBiasShare * rangeM);
        double unit = FireControl.RangefinderUnitM(s.BaseM, o, FireControl.Magnification(o, s.Support, s.HeightM), rangeM);
        return (crew.Operator * settings.GunneryCombat * unit, s.Bias * settings.RangefinderBiasUnits * unit);
    }

    /// <summary>Where the track says the target will be when a shell fired now from a muzzle comes down: the time of
    /// flight iterated through the range table, from last second's.</summary>
    Vec3d Predict(ref Fc fc, BallisticTable table, Vec3d muzzle, long now, Vec3d ownPosition, Vec3d ownVelocity)
    {
        double h0 = muzzle.Length - Earth.Radius;
        var computer = fc.Fit.Computer;
        fc.Tracker.At(computer, now + (long)(fc.TofS * 1000), now, ownPosition, ownVelocity, out var p, out _);
        for (int i = 0, n = fc.TofS > 0 ? 2 : 3; i < n; i++)
        {
            double e = table.ElevationFor(Earth.Distance(muzzle, p), h0, 1, fc.ElevationDeg);
            if (double.IsNaN(e))
                break;
            fc.ElevationDeg = e;
            fc.TofS = table.Fall(e, h0).Tof;
            fc.Tracker.At(computer, now + (long)(fc.TofS * 1000), now, ownPosition, ownVelocity, out p, out _);
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
        int n = 0, lead = -1, bearingGuns = 0, bearingMounts = 0;
        for (int j = 0; j < mounts.Length; j++)
        {
            ref readonly var mr = ref cls.Mounts[mounts[j]];
            ref readonly var ms = ref store.Combat.Mount(slot, mounts[j]);
            // loaded, with rounds; ordered on the target (not parked at its traverse's end) inside its arcs; and there
            double tol = settings.LayToleranceDeg;
            bool bears = ms.Rounds > 0 && cond.Lays(mounts[j], fc.Local) && fc.OrderElevationDeg <= mr.MaxElevationDeg && CombatStore.InArcs(cls, mr, fc.OrderBearingDeg);
            (bearingGuns, bearingMounts) = bears ? (bearingGuns + cond.Guns(mounts[j]), bearingMounts + 1) : (bearingGuns, bearingMounts);
            firing[j] = bears && ms.LoadedMs <= now &&
                        Math.Abs(Wrap(ms.TrainTo - fc.OrderBearingDeg)) <= tol &&
                        Math.Abs(CombatStore.BearingAt(ms, mr, now) - ms.TrainTo) <= tol && Math.Abs(CombatStore.ElevationAt(ms, mr, now) - ms.ElevTo) <= tol;
            // each mount fires the first load of the magazine order it has rounds of
            fireLoad[j] = firing[j] ? store.Combat.NextLoad(slot, mounts[j]) : -1;
            firing[j] &= fireLoad[j] >= 0;
        }
        // a partial salvo (F6: ranging, or a half firing for effect), the mounts taken from where the last one left off, so
        // the groups alternate
        var phase = fc.Phase;
        var dis = settings.FireDisciplines[disciplines[ship.Side]];
        bool independent = Independent(dis, cls, mounts, fc.RangeM);
        var (need, quota) = independent ? (int.MaxValue, 1) : Partial(dis, phase, bearingGuns, bearingMounts, fc.Half);
        int next = fc.Group, taken = 0;
        for (int k = 0; k < mounts.Length; k++)
        {
            int j = (fc.Group + k) % mounts.Length;
            if (!firing[j])
                continue;
            if (n >= need || taken >= quota)
            {
                firing[j] = false;
                continue;
            }
            n += cond.Guns(mounts[j]);
            taken++;
            next = j + 1;
            if (lead < 0)
                lead = loads.Loads(fc.Battery)[fireLoad[j]].ShellType;
        }
        if (n == 0 || need < int.MaxValue && n < need)
            return;   // none, or a ranging salvo short of its guns (one not laid at the instant): the next second times it again
        if (need < int.MaxValue || quota < int.MaxValue)
            fc.Group = next % mounts.Length;
        if (quota < int.MaxValue && !independent)
            fc.Half ^= 1;
        // the battery's computer works in its lead shell's table: the first firing mount's (the next salvos lay with it)
        fc.Table = shells.TableOf(loads, fc.Battery, lead);

        var computer = fc.Fit.Computer;
        var mount = fc.Fit.Mount;
        var crew = settings.GunneryCrews[(int)crews[ship.Side]];
        int tableIndex = fc.Table;
        var table = shells.Ballistics[tableIndex];
        var frame = ship.Frame;
        var velocity = ship.Forward * ship.Speed;
        var muzzle0 = Muzzle(cls, frame, mounts[0]);
        double h0 = muzzle0.Length - Earth.Radius;
        var p = Aim(ref fc, f, table, muzzle0, now, ship.Position, velocity, store[tslot], crew);
        bool pointBlank = fc.PointBlank && phase == FirePhase.Effect;   // the phase as the salvo was thinned (Run: point blank fires for effect)
        var up = p.Normalized();
        var line = p - ship.Position;
        var u = (line - up * Vec3d.Dot(line, up)).Normalized();   // along the line of fire, at the target
        var w = Vec3d.Cross(u, up);                                  // to its right
        double range = Earth.Distance(ship.Position, p), combat = settings.GunneryCombat;
        double e0 = table.ElevationFor(range, h0, 1, fc.ElevationDeg);
        if (double.IsNaN(e0))
            return;
        double perRad = table.RangePerDeg(e0, h0) * 180 / Math.PI;

        // the range correction from the spots' bracket (a ladder's salvo stepped about it), then the salvo's common error
        // (fire_control_ref.py engage(), "fire")
        fc.CorrR = fc.Bracket.Aim(now, fc.DriftMs);
        double ladder = phase == FirePhase.Ranging && dis.LadderStepM > 0 && !pointBlank ? (fc.Rung - 0.5 * (dis.InFlight - 1)) * dis.LadderStepM : 0;
        var r = stream.Derive((ulong)f).Derive((ulong)fc.Generation).Derive(0x5A1u).Derive((ulong)fc.Salvos);
        double layRad = mount.LayMil * FireControl.Mil * crew.Lay * combat;
        double eEl = mount.DirectorFiring ? r.NextGaussian() * layRad * perRad : 0, eTr = mount.DirectorFiring ? r.NextGaussian() * layRad * range : 0;
        bool turning = Math.Abs(double.RadiansToDegrees(ship.YawRate)) > settings.OwnTurnYawDegS;
        double kickR = turning ? r.NextGaussian() * computer.OwnTurnKickM : 0, kickL = turning ? r.NextGaussian() * computer.OwnTurnKickM : 0;
        double eSr = r.NextGaussian() * combat * computer.SalvoPct / 100 * range, eSd = r.NextGaussian() * combat * mount.SalvoDeflMil * FireControl.Mil * range;
        double dr = (pointBlank ? 0 : fc.CorrR + ladder) + eEl + eSr + fc.BiasR * computer.ResidPct / 100 * range + kickR;
        double dl = fc.CorrD + eTr + eSd + fc.BiasD * computer.DeflBiasMil * FireControl.Mil * range + kickL;
        var (sr, sd) = FireControl.Dispersion(row.PatternPct, range + dr, n, settings.DispersionDeflectionRatio);
        if (!mount.DirectorFiring)
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
            Alive = true, Generation = sv.Generation + 1, Fc = f, FcGeneration = fc.Generation, Target = fc.Target, CorrR = fc.CorrR + ladder, CorrD = fc.CorrD,
            PatternM = 4 * sr, RangeM = range, SpotSigmaM = spot, Common = 0.5 * spot * r.NextGaussian(), U = u, W = w, MinX = double.MaxValue,
            MaxX = double.MinValue, TargetLengthM = store[tslot].Handling.Class.Silhouette.LengthM, FiredMs = now,
            AimErrM = Vec3d.Dot(p - truth, u), AimErrAcrossM = Vec3d.Dot(p - truth, w), Phase = phase, LadderM = ladder, Shift = fc.Shift,
            PointBlank = pointBlank, TrueRangeM = Earth.Distance(ship.Position, tgt.Position), PbCorrR = fc.PbCorrR,
        };
        int salvo = shells.NewSalvo();
        for (int j = 0; j < mounts.Length; j++)
        {
            if (!firing[j])
                continue;
            ref readonly var mr = ref cls.Mounts[mounts[j]];
            int barrels = cond.Guns(mounts[j]);   // its guns out don't fire (E4)
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
            store.Combat.Fired(slot, cls, mounts[j], fireLoad[j], barrels, now, crew.Load);
        }
        sv.Left = sv.Shells;
        if (sv.Shells == 0)
        {
            FreeSalvo(si);   // no shell got away (every one laid past its reach): no salvo
            return;
        }
        fc.Salvos++;
        fc.Shells += sv.Shells;
        fc.LastSalvoMs = now;
        fc.InFlight++;
        fc.WaitMs = now + (long)(tof * 1000) + settings.SpotDelayMs + settings.RangingWaitMarginMs;
        if (phase == FirePhase.Ranging)
            (fc.RangingSalvos, fc.RangingShells, fc.Rung) = (fc.RangingSalvos + 1, fc.RangingShells + sv.Shells, fc.Rung + 1);
        log.Add(new CombatEntry(now, CombatEvent.Fired, fc.Shooter, fc.Battery, fc.Target, sv.Shells));
        // the main battery's own shock on its breakers (E4; damage/13 §3.4)
        if (row.Kind == Battery.Main)
            for (int j = 0; j < mounts.Length; j++)
                if (firing[j])
                {
                    world.Damage.OwnGuns(world, slot, mounts[j], ref r);
                    break;
                }
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
                sv.SumY / Math.Max(sv.Landed, 1), sv.AimErrM, sv.AimErrAcrossM, sv.PointBlank ? sv.PbCorrR * sv.TrueRangeM : sv.CorrR, sv.Hits, straddle, sv.Phase, sv.LadderM,
                sv.TrueRangeM, sv.PointBlank));
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

    /// <summary>The spotter's judgement on a salvo that's down, if he sees the target: over, short or straddling, into
    /// the bracket (and so the next salvo's range correction) and the phase, and its deflection.</summary>
    void Spot(World world, int si)
    {
        ref var sv = ref salvos[si];
        ref var fc = ref fcs[sv.Fc];
        var store = ships.Store;
        bool own = fc.Alive && fc.Generation == sv.FcGeneration;
        if (own)
            fc.InFlight = Math.Max(fc.InFlight - 1, 0);
        int slot = own && fc.Target == sv.Target ? store.SlotOf(fc.Shooter) : -1;
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
                var air = AirAt(world, ship.Position);
                for (int k = 0; k < fc.SensorCount && !sees; k++)
                {
                    ref readonly var s = ref sensors[sv.Fc * MaxSensors + k];
                    sees = MarginOf(ship.Handling.Class, s, fc.Fit.Optics, frame.ToWorld(s.X, s.Y, 0), ship.HeadingDeg, target, air) >= 1;
                }
            }
            var sign = sv.Hits > 0 || sv.Over > 0 && sv.Short > 0 ? FireControl.Sign.Straddle : sv.Over > 0 ? FireControl.Sign.Over : FireControl.Sign.Short;
            long now = world.Time.Ms;
            if (sees && !sv.PointBlank)
            {
                // each spot against the correction its salvo was fired with; a crew may pyramid, putting it on the one in
                // force (F6: the green most often)
                var dis = settings.FireDisciplines[disciplines[ship.Side]];
                var rp = stream.Derive(0x5B09u).Derive((ulong)si).Derive((ulong)sv.Generation);
                bool pyramid = rp.NextDouble() < settings.GunneryCrews[(int)crews[ship.Side]].Pyramid;
                double before = fc.CorrR;
                fc.Bracket.Spot(sign, pyramid ? fc.CorrR + sv.LadderM : sv.CorrR - (fc.Shift - sv.Shift), now, fc.DriftMs, sv.PatternM,
                    Math.Max(settings.BracketFloorM, settings.BracketFloorPatterns * sv.PatternM));
                fc.CorrR = fc.Bracket.Aim(now, fc.DriftMs);
                // the rate spot: two moves the same way, and the range rate takes a share of this one a minute
                int move = Math.Abs(fc.CorrR - before) < 1 ? 0 : Math.Sign(fc.CorrR - before);
                if (dis.RateSpotShare > 0 && move != 0 && move == fc.LastMove)
                    fc.Tracker.CorrectRate(sv.U, dis.RateSpotShare * (fc.CorrR - before) / 60);
                fc.LastMove = move;
                if (move != 0)
                    fc.SettleMs = now + (long)(1000 * settings.LayerSettleS);
                NextPhase(ref fc, sign, now, dis.MissesToRange);
                var r = stream.Derive(0x5B08u).Derive((ulong)si).Derive((ulong)sv.Generation);
                double defl = sv.SumY / Math.Max(sv.Landed, 1) + settings.SpotDeflectionMil * FireControl.Mil * sv.RangeM * r.NextGaussian();
                if (Math.Abs(defl) > 0.25 * sv.PatternM)
                    fc.CorrD = (pyramid ? fc.CorrD : sv.CorrD) - settings.SpotDeflectionGain * defl;
            }
            else if (sees && sv.Hits == 0)
            {
                // point blank: the layers walk their own fall onto her (the user, 2026-10-18), reading how far over or short
                // and off the line it fell (k·R²/h from their height: a few metres close in); a hit, and they hold the lay
                // (this branch isn't taken); a miss, they take half of it off, as magnitude spots go (half gain, a dead band
                // of half a pattern, so the gun's own scatter isn't chased: fire-control-research.md §9)
                var r = stream.Derive(0x5B0Bu).Derive((ulong)si).Derive((ulong)sv.Generation);
                double x = sv.SumX / Math.Max(sv.Landed, 1) + sv.SpotSigmaM * r.NextGaussian();
                if (Math.Abs(x) > 0.5 * sv.PatternM)
                    fc.PbCorrR = sv.PbCorrR - 0.5 * x / Math.Max(sv.TrueRangeM, 1);
                double defl = sv.SumY / Math.Max(sv.Landed, 1) + settings.SpotDeflectionMil * FireControl.Mil * sv.RangeM * r.NextGaussian();
                if (Math.Abs(defl) > 0.25 * sv.PatternM)
                    fc.CorrD = sv.CorrD - settings.SpotDeflectionGain * defl;
            }
            Spotted?.Invoke(new SpotReport(sv.FiredMs, fc.Shooter, fc.Battery, sees, sign, fc.Bracket.LoAt(now, fc.DriftMs), fc.Bracket.HiAt(now, fc.DriftMs),
                fc.CorrR, fc.Phase));
        }
        FreeSalvo(si);
    }

    /// <summary>A spot's say on the phase (F6): a straddle while ranging opens fire for effect; in fire for effect a run
    /// of spots without one goes back to ranging, the bracket opened the way they all pointed if they did.</summary>
    void NextPhase(ref Fc fc, FireControl.Sign sign, long now, int missesToRange)
    {
        if (sign == FireControl.Sign.Straddle)
        {
            if (fc.Phase == FirePhase.Ranging && fc.EffectMs < 0)
                fc.EffectMs = now;
            if (fc.Phase == FirePhase.Ranging)
                fc.Phase = FirePhase.Effect;
            (fc.Misses, fc.MissOver, fc.MissShort) = (0, 0, 0);
            return;
        }
        if (fc.Phase != FirePhase.Effect)
            return;
        fc.Misses++;
        if (sign == FireControl.Sign.Over)
            fc.MissOver++;
        else
            fc.MissShort++;
        if (fc.Misses < missesToRange)
            return;
        if (fc.MissOver == 0 || fc.MissShort == 0)
            fc.Bracket.OpenToward(sign, now, fc.DriftMs);
        (fc.Phase, fc.Misses, fc.MissOver, fc.MissShort, fc.Rung, fc.Bracket.Step) = (FirePhase.Ranging, 0, 0, 0, 0, FirstStep(fc));
    }

    /// <summary>The solution disturbed (own ship turning, the target seen to turn): a battery firing for effect goes back
    /// to ranging, its bracket opened round its middle to the first step.</summary>
    void Disturbed(ref Fc fc, long now)
    {
        fc.Bracket.Step = FirstStep(fc);
        fc.Bracket.Widen(now, fc.DriftMs, fc.Bracket.Step);
        if (fc.Phase == FirePhase.Effect)
            (fc.Phase, fc.Misses, fc.MissOver, fc.MissShort) = (FirePhase.Ranging, 0, 0, 0);
        fc.Rung = 0;
    }

    /// <summary>The first step beyond a bound (F6): twice the error the computer expects of its own solution over the time
    /// of flight (its doubt of the target's position and rate along the line of fire, and its residual bias), never more
    /// than its own first step (the RN's 400 yd) nor under the bracket's floor.</summary>
    double FirstStep(in Fc fc)
    {
        var c = fc.Fit.Computer;
        double rate = fc.DriftMs * fc.TofS, resid = c.ResidPct / 100 * fc.RangeM;
        double sigma = Math.Sqrt(fc.PosSigmaM * fc.PosSigmaM + rate * rate + resid * resid);
        return Math.Clamp(2 * sigma, settings.BracketFloorM, Math.Max(c.FirstStepM, settings.BracketFloorM));
    }

    /// <summary>Whether a battery fires independently (F6d): its discipline's range for it, and its guns quick-firers.</summary>
    bool Independent(FireDiscipline dis, ShipClass cls, ReadOnlySpan<int> mounts, double rangeM) =>
        dis.IndependentWithinM > 0 && rangeM < dis.IndependentWithinM && cls.Mounts[mounts[0]].ReloadS <= settings.QuickFirerReloadS;

    /// <summary>A partial salvo by the discipline, of the mounts that bear (loaded or not), as the guns it wants and the
    /// mounts it may take (int.MaxValue: no limit, a full salvo): ranging, the discipline's share of the guns, at least
    /// its minimum (enough to spot a wild shot by; all of them if fewer); firing for effect in halves, half the mounts,
    /// the larger half and the smaller in turn (five turrets: three, then two).</summary>
    static (int Guns, int Mounts) Partial(FireDiscipline dis, FirePhase phase, int bearingGuns, int bearingMounts, int half)
    {
        if (phase == FirePhase.Ranging && dis.GunsShare < 1)
            return (Math.Min(bearingGuns, Math.Max(dis.GunsMin, (int)Math.Ceiling(dis.GunsShare * bearingGuns - 1e-9))), int.MaxValue);
        if (phase == FirePhase.Effect && dis.EffectHalves && bearingMounts > 1)
            return (int.MaxValue, half == 0 ? (bearingMounts + 1) / 2 : bearingMounts / 2);
        return (int.MaxValue, int.MaxValue);
    }

    static double Mod(double d) => (d % 360 + 360) % 360;

    static double Wrap(double d) => d - 360 * Math.Round(d / 360);

    // ------------------------------------------------------------------ reading it

    /// <summary>An engaged battery's counts so far, for tools and tests: salvos, shells, hits, straddles, when it first
    /// hit and straddled (ms, -1 if not yet), when its track started, what it's doing and whether it's in local control;
    /// its fire discipline's phase, the salvos and shells it fired ranging, and when it first fired for effect (-1).</summary>
    public readonly record struct Tally(int Salvos, int Shells, int Hits, int Straddles, long FirstHitMs, long FirstStraddleMs, long TrackMs,
        BatteryState State, bool Local, FirePhase Phase, int RangingSalvos, int RangingShells, long EffectMs);

    /// <summary>A ship's battery's tally, if it's engaged.</summary>
    public Tally? TallyOf(int shooter, int battery)
    {
        for (int i = 0; i < highFc; i++)
            if (fcs[i].Alive && fcs[i].Shooter == shooter && fcs[i].Battery == battery)
            {
                ref readonly var fc = ref fcs[i];
                return new Tally(fc.Salvos, fc.Shells, fc.Hits, fc.Straddles, fc.FirstHitMs, fc.FirstStraddleMs, fc.Tracking ? fc.TrackMs : -1, fc.State, fc.Local,
                    fc.Phase, fc.RangingSalvos, fc.RangingShells, fc.EffectMs);
            }
        return null;
    }

    /// <summary>Where an engaged battery's computer has its target at a time (its track carried on to it), and the
    /// velocity it holds, for benches (fire-control.md F1: the prediction's error against where the target went); false
    /// while it has no track.</summary>
    public bool TrackAt(int shooter, int battery, long ms, out Vec3d position, out Vec3d velocity)
    {
        for (int i = 0; i < highFc; i++)
            if (fcs[i].Alive && fcs[i].Shooter == shooter && fcs[i].Battery == battery && fcs[i].Tracking && ships.Store.SlotOf(shooter) is >= 0 and var slot)
            {
                var own = ships.Store[slot];
                fcs[i].Tracker.At(fcs[i].Fit.Computer, ms, ships.Store.NowMs, own.Position, own.Forward * own.Speed, out position, out velocity);
                return true;
            }
        (position, velocity) = (default, default);
        return false;
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
        if (disciplinesSet)
            for (int s = 0; s < disciplines.Length; s++)
                if (disciplines[s] != 0)
                {
                    h.Add(s);
                    h.Add(disciplines[s]);
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
            fc.Tracker.Hash(ref h);
            h.Add(fc.CorrR);
            h.Add(fc.CorrD);
            h.Add(fc.Bracket.Lo);
            h.Add(fc.Bracket.Hi);
            h.Add((int)fc.Phase);
            h.Add(fc.InFlight);
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
            (v.Shooter[n], v.Battery[n], v.Target[n], v.State[n], v.Phase[n]) = (fc.Shooter, fc.Battery, fc.Target, fc.State, fc.Phase);
            (v.Salvos[n], v.Shells[n], v.Hits[n], v.Level[n], v.Local[n]) = (fc.Salvos, fc.Shells, fc.Hits, (byte)fc.Level, fc.Local);
            n++;
        }
        v.Count = n;
        log.Fill(snap.CombatLog, 64);
    }
}
