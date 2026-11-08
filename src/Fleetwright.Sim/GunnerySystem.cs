using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim;

/// <summary>What the gunnery system understands (<see cref="GunnerySystem"/>).</summary>
public enum GunneryCommand : ushort
{
    Engage = 1,
    Cease = 2,
    SetExperience = 3,
    SetDiscipline = 4,
    Load = 5,
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
    /// <summary>Laid and ready, the gunnery officer holds fire: a shell isn't worth firing at her from here
    /// (docs/battle/balance.md BL1p).</summary>
    Holding,
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
    /// walking the fall onto her; back to ranging when the range is clearly lost or the solution is disturbed.</summary>
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
/// highest eye) judges it over, short or straddling (σ = k·R²/h) and sees its deflection; ranging, the range spots make a
/// bracket in correction space (<see cref="FireControl.Bracket"/>), each against the correction its salvo was fired with,
/// and the next salvo goes into it; firing for effect, they walk the correction (<see cref="Walk"/>).</item>
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
        /// <summary>A computer with no range clock (a hand plot, fc-ladder-check.md FL4): when it last plotted a reading, and
        /// the range it passed to the sights then (held till the next), as of which plot.</summary>
        public long PlotMs, HeldMs;
        public double HeldRangeM;
        public bool Tracking, SalvoPending;
        public FcTracker Tracker;
        /// <summary>When a judgement looks at the target again, and the eye's misjudgement of her speed and course, in the
        /// computer's priors' sigmas, drawn once a target: each look at her misjudges her the same way.</summary>
        public long JudgeMs;
        public double GuessSpeedZ, GuessCourseZ;
        public double MvZ, DensityZ, CorrR, CorrD, SpotHeightM;
        // the machine's measuring errors in the day's ballistics, in its own σ (ES3: the battery's, as the residual bias
        // was), and its doubt of them at the range it last fired at (a share of the range; rad across)
        public Vec3d WindZ;
        public double BallR, BallD;
        /// <summary>What its battery carried from its last target (docs/battle/balance.md BL1ad): the correction in range it
        /// opens its bracket about, a share of the range, and its doubt of the residual bias in range (a share) and in line
        /// (rad), the computer's own residual bias with nothing carried.</summary>
        public double CarryR, ResidShare, DeflDoubtRad;
        /// <summary>A machine that takes own ship's motion in (helm-free, or in true motion): own ship as it reckons her
        /// (docs/battle/fc-ladder-check.md FL1), her course by the gyro's repeat (at once, or late by its lag where a man
        /// follows the gyro's pointer) and her speed by log (late by its lag), her place dead reckoned from them; as of when
        /// (ms).</summary>
        public Vec3d OwnHatP, OwnHatV;
        public double OwnHatHeadingDeg, OwnHatSpeed;
        /// <summary>A stable vertical's own error (docs/battle/fc-ladder-check.md FL7): its tilt off the true vertical, east
        /// and north (rad), the way it drifts while its erection is cut out in her turn (a unit vector, east and north),
        /// whether cut out, as of when (ms).</summary>
        public double SvTiltE, SvTiltN, SvDriftE, SvDriftN;
        public bool SvCut;
        public long SvMs;
        public long OwnHatMs;
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
        public long PbLeftMs;
        public FireControl.Bracket Bracket;
        public double DriftMs;
        /// <summary>The computer's doubt of the target's position along the line of fire (m), and the range to its aim
        /// (m), for the first step.</summary>
        public double PosSigmaM, RangeM;
        /// <summary>Its salvos in the air or down and not yet spotted; while ranging the next waits for them, or until
        /// <see cref="WaitMs"/> (a spot that never came).</summary>
        public int InFlight;
        public long WaitMs;
        /// <summary>In fire for effect, the spots in a row that weren't straddles.</summary>
        public int Misses;
        /// <summary>In fire for effect, the spots' running mean (<see cref="Walk"/>): its first step (m), the spots it has
        /// shrunk by, which way it last stepped (-1 down, +1 up, 0 none yet), and how far (m) and in how many steps it has gone
        /// that way in a row.</summary>
        public double WalkStep, WalkRunM;
        /// <summary>Ranging, the corrections the last over and the last short were fired with (NaN none yet): crossed
        /// within a pattern, she's found.</summary>
        public double OverAt, ShortAt;
        public int WalkN, WalkDir, WalkRun;
        /// <summary>Which mount the next partial salvo starts from (the groups alternate), and the tracker's turns seen.</summary>
        public int Group, Turns;
        /// <summary>A ladder's salvos fired in its group so far (the discipline's ladder: the group goes at once, stepped).</summary>
        public int Rung;
        /// <summary>Firing for effect in halves, which half goes next (0 the larger).</summary>
        public int Half;
        /// <summary>Whether its gunnery officer has its fire pared back (BL1p), and since when he's held the salvo due for her
        /// turn to ease (-1: not holding).</summary>
        public bool Pared;
        public long SpoiledMs;
        /// <summary>Her course as it was last set on a plain computer's Dumaresq, degrees (NaN: not yet): past
        /// <see cref="SimSettings.OwnCourseStaleDeg"/> from it, its rates are stale and its battery ranges again.</summary>
        public double SetCourseDeg;
        public int RangingSalvos, RangingShells;
        public long EffectMs, LastSalvoMs, SettleMs;
        /// <summary>Whether the computer set its last reading aside as one she couldn't have made.</summary>
        public bool SetAside;
        /// <summary>The last spot's call (a FireControl.Sign; -1 none yet).</summary>
        public int LastSign;
        public int Salvos, Shells, Hits, Straddles, Arm, SensorCount, SensorSet, PendHead, PendCount;
        /// <summary>The shooter's condition's version its instruments were picked at (damage: a director lost).</summary>
        public int Condition;
        /// <summary>The fire-control units it reads from (a bit a unit), and whether a working one of its own was held by a
        /// battery on another target when they were picked (so it picks again when one is let go).</summary>
        public ulong Units;
        public bool Short, Repick;
        public long FirstHitMs, FirstStraddleMs;
        public BatteryState State;
        /// <summary>Her roll in the sea as her layers meet it (docs/battle/balance.md BL1b), worked out again when the sea, her
        /// heading, speed or GM moves a step (<see cref="RollKey"/>); whether they lay on the roll (firing as it brings the
        /// sight on, once a roll) rather than following it; and when it next brings the sight on for the salvo due.</summary>
        public RollState Roll, Pitch;
        /// <summary>The best margin over the air its sensors had on the target this second (BL1i: the layer's and the
        /// spotter's eyes work through the same air).</summary>
        public double Margin;
        public long RollKey, RollMs;
        public bool OnRoll;
    }

    /// <summary>A rangefinder or an eye a battery reads from: the class's eye (-1: a mount's layer), where it stands and
    /// on what, its height and base, its bearing's error (mil, by its position), its next reading and its own bias (in
    /// sigmas); ranging by eye, the slow wander of his judgement (in sigmas) and when it stood so.</summary>
    struct Sensor
    {
        public int Eye;
        public InstrumentSupport Support;
        public double X, Y, HeightM, BaseM, BearingMil;
        /// <summary>Its errors, drawn once when it's put on a target (the user, 2026-11-02: "once per fresh target"; a
        /// draw each reading, or a wander, moved the guns with them): the instrument's own bias, its man's misjudgement of
        /// her range and of her bearing, each in sigmas.</summary>
        public double Bias, RangeZ, BearingZ;
        /// <summary>Its point among its ship's on the sea (<see cref="SeaMotion"/>: a mount's for a layer by eye).</summary>
        public int SeaPoint;
        /// <summary>Its cuts on the plot so far, how far they lie off the computer's line on average in range (m) and
        /// bearing (deg), and their weights (1/sigma² of its last reading).</summary>
        public int Cuts;
        public double OffRangeM, OffBearingDeg, WRange, WBearing;
        public long NextMs;
    }

    // ------------------------------------------------------------------ the sea over her (ES4)

    // the tick's ships whose sea has moved a step (ES4: docs/battle/error-sources.md), and the tick's stamp
    int[] seaDue = new int[16];
    int seaTick;

    readonly struct SeawayJob(GunnerySystem g, World world) : IJob
    {
        public void Run(int from, int to, int worker)
        {
            var store = g.ships.Store;
            for (int i = from; i < to; i++)
                SeaAboard.Work(g.settings, world.Sea, store.Combat.Sea(g.seaDue[i]));
        }
    }

    /// <summary>The tick's batteries' ships whose sea (<see cref="SeaMotion"/>) has moved a step since it was worked out,
    /// each once (on the sim thread, in battery order): the sea, her heading (10°), speed (0.5 m/s) or GM (5%); their sea
    /// then worked out on the workers, from the step's values, so it's the same whenever it's asked (any compression).</summary>
    void Seaways(World world, int batteries)
    {
        var store = ships.Store;
        seaTick++;
        int n = 0;
        for (int i = 0; i < batteries; i++)
        {
            int slot = store.SlotOf(fcs[due[i]].Shooter);
            if (slot < 0)
                continue;
            var sm = store.Combat.Sea(slot);
            if (sm.Stamp == seaTick)
                continue;
            sm.Stamp = seaTick;
            var ship = store[slot];
            var cls = ship.Handling.Class;
            var cond = store.Combat.Condition(slot);
            double gm0 = Math.Max(cls.Hydrostatics.GmT, 1e-6);
            int h = (int)(Mod(ship.HeadingDeg) / 10) % 36, v = (int)Math.Clamp(ship.Speed / 0.5, 0, 4095), g = (int)Math.Clamp(Math.Round(20 * cond.GmM / gm0), 1, 4095);
            long key = ((long)world.Sea.Id << 40) | ((long)h << 24) | ((long)v << 12) | (uint)g;
            if (key == sm.Key)
                continue;
            (sm.Key, sm.HeadingDeg, sm.SpeedMs, sm.Gm) = (key, h * 10 + 5, (v + 0.5) * 0.5, g / 20.0);
            if (n == seaDue.Length)
                Array.Resize(ref seaDue, 2 * n);
            seaDue[n++] = slot;
        }
        world.Workers.For(new SeawayJob(this, world), n, 1, 4);
    }

    /// <summary>Whether a wetting at this rate (a second) has one of her points out now (ES4): each clears in its own time,
    /// so the time it's out is 1 − exp(−rate·clear) (<see cref="SeaAboard.Out"/>), its windows of that length each drawn
    /// whether a wetting fell in it (the sea's own randomness: which second a wave comes aboard), the same at any
    /// compression. The ladder's spray rung scales the rate.</summary>
    bool Wet(int shooter, int code, double rate, double clearS, long now)
    {
        double k = settings.GunneryErrors.Spray;
        if (!(k > 0) || !(rate > 0))
            return false;
        double p = SeaAboard.Out(rate * k, clearS);
        if (p < 1e-9)
            return false;
        if (p >= 1)
            return true;
        long window = now / Math.Max((long)(clearS * 1000), 1);
        return stream.Derive(0x5EA5u).Derive((ulong)(uint)shooter).Derive((ulong)code).Derive((ulong)window).NextDouble() < p;
    }

    /// <summary>Whether a mount's guns are out now for the sea coming through their ports (ES4; docs/battle/balance.md BL1f's
    /// casemates under the one law): green water reaching their axis, each wetting <see cref="SimSettings.GreenWaterClearS"/>.</summary>
    bool GunWet(int shooter, int slot, in ShipState ship, ShipClass cls, ShipCondition cond, int mount, long now)
    {
        var sw = ships.Store.Combat.Sea(slot);
        if (sw.Key < 0)
            return false;
        int p = SeaAboard.MountPoint(mount);
        double rate = SeaAboard.Green(settings, cls, cond.Attitude, ship.Speed, sw.X[p], sw.Y[p], sw.M0[p], sw.M2[p], SeaAboard.GunAxis(cls, mount));
        return Wet(shooter, mount, rate, settings.GreenWaterClearS, now);
    }

    /// <summary>Whether glass at one of her points (z, the class's heights) is blinded now (ES4): green water or her bow's
    /// spray reaching it, each wetting <see cref="SimSettings.SprayClearS"/> to clear.</summary>
    bool GlassWet(World world, int shooter, int slot, in ShipState ship, ShipClass cls, ShipCondition cond, int p, double z, long now)
    {
        var sw = ships.Store.Combat.Sea(slot);
        if (p < 0 || sw.Key < 0)
            return false;
        var wx = world.Weather.At(ship.Position, world.Time);
        double rate = SeaAboard.Green(settings, cls, cond.Attitude, ship.Speed, sw.X[p], sw.Y[p], sw.M0[p], sw.M2[p], z) +
                      SeaAboard.Spray(settings, cls, cond.Attitude, ship.Speed, ship.HeadingDeg, wx, sw.M0[0], sw.M2[0], sw.X[p], sw.Y[p], z);
        return Wet(shooter, 0x1000 + p, rate, settings.SprayClearS, now);
    }

    /// <summary>A sensor at one of the class's eyes.</summary>
    static Sensor SensorAt(ShipClass cls, int eye)
    {
        ref readonly var e = ref cls.Eyes[eye];
        return new Sensor
        {
            Eye = eye, Support = e.Support, X = e.X, Y = e.Y, HeightM = e.HeightM, BaseM = e.RangefinderM, BearingMil = SightModel.BearingSigmaRad(e) / FireControl.Mil,
            SeaPoint = SeaAboard.EyePoint(cls, eye),
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
        public double CorrR, CorrD, PatternM, PatternAcrossM, RangeM, SpotSigmaM, Common, SumY, MinX, MaxX, TargetLengthM, SumX, AimErrM, AimErrAcrossM;
        public long FiredMs;
        public FirePhase Phase;
        /// <summary>Whether it was laid point blank (its fall says nothing to the bracket: its range was the layers' own).</summary>
        public double TrueRangeM, PbCorrR;
        public bool PointBlank;
        /// <summary>Her rate of turn as it was fired, °/s (positive to starboard): fired under helm or steady, for tools.</summary>
        public float YawDegS;
        /// <summary>Its ladder's step off the bracket's aim, m (in its correction).</summary>
        public double LadderM;
        public Vec3d U, W;
        /// <summary>Whose splash (BL1c): its calibre, mm, its ship (an entity), and when its first and last shells came down.</summary>
        public double CalibreMm;
        public int Shooter;
        public long FirstDownMs, LastDownMs;
    }

    /// <summary>A salvo's fall, for tools (not world state): when it was fired and by whom, its range and shells, where
    /// its shells fell on average against the target (along the line of fire and to its right, m), how far the aim
    /// point was from where the target truly went (the track's error, as the target's course and speed carry it on),
    /// the range correction it was fired with (its ladder's step in it; laid point blank, what its layers had walked onto
    /// their sight), its hits, whether it straddled, the phase it was fired in, her true range, whether point blank, and
    /// where the guns were laid against where she went, along the line (the aim with its correction, before the salvo's own
    /// errors), m; and her rate of turn as she fired it, °/s (docs/battle/balance.md BL1m: fire under helm).</summary>
    public readonly record struct SalvoReport(long FiredMs, int Shooter, int Battery, double RangeM, int Shells, double MeanX, double MeanY, double AimErrM,
        double AimErrAcrossM, double CorrR, int Hits, bool Straddle, FirePhase Phase, double LadderM, double TrueRangeM, bool PointBlank, double LaidErrM,
        int Target = 0, double YawDegS = 0);

    /// <summary>A salvo's spot, for tools: the salvo (by when it was fired), whether the spotter saw its fall and what he
    /// judged it, the bracket after it (m; infinite: open) and the correction the next salvo goes with, and the phase
    /// after it; the computer's doubt of the range rate (m/s) and of her place along the line (m), and its tracker; the
    /// spotter's error over or short of a splash (m) and his height (m).</summary>
    public readonly record struct SpotReport(long FiredMs, int Shooter, int Battery, bool Seen, FireControl.Sign Sign, double Lo, double Hi, double Next,
        FirePhase Phase, bool Told = true, double DriftMs = 0, double PosSigmaM = 0, int Tracker = 0, double SpotSigmaM = 0, double SpotHeightM = 0);

    /// <summary>Whether a chief battery's shells have fallen near a ship since a time (docs/battle/balance.md BL5f: her
    /// lookouts see the big splashes about her): a fall in her ring of a calibre her shooter's chief battery fires.</summary>
    public bool ChiefFallsNear(int slot, long sinceMs)
    {
        if (slot < 0 || slot >= splashHead.Length)
            return false;
        var store = ships.Store;
        for (int k = slot * SplashRing, end = k + SplashRing; k < end; k++)
        {
            if (splashMs[k] < sinceMs || splashMs[k] <= 0 || store.SlotOf(splashShooter[k]) is not (int ss and >= 0))
                continue;
            var cls = store[ss].Handling.Class;
            for (int b = 0; b < cls.Batteries.Length; b++)
                if (cls.Batteries[b].Chief && Math.Abs(cls.Batteries[b].CalibreMm - splashCal[k]) < 1)
                    return true;
        }
        return false;
    }

    /// <summary>A ship's battery's best margin over the air on its target as of its last second (BL1i; tools), 0 if it isn't
    /// engaged.</summary>
    public double MarginOf(int shooter, int battery)
    {
        for (int i = 0; i < highFc; i++)
            if (fcs[i].Alive && fcs[i].Shooter == shooter && fcs[i].Battery == battery)
                return fcs[i].Margin;
        return 0;
    }

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
    // each battery's guns' own layers' margins on its target at its last second (ES6: a local layer lays by his own view
    // through the smoke, not the director's), NaN where the smoke wasn't asked, by the battery's mount order: its first
    // MaxLayers mounts' (a battery of more lays the rest by the battery's margin), grown with the engagements' pool
    const int MaxLayers = 32;
    float[] layerMargin;
    FcReading[] pending;
    readonly Stack<int> freeFc;
    int highFc;
    Salvo[] salvos;
    readonly Stack<int> freeSalvo;
    int highSalvo;
    // the splashes about each ship (BL1c), by her slot: a ring of the last few, when each came down, its calibre and the
    // ship that fired it, and where it came down with its column's peak (ES2: 0 for a hit, which throws none); grown with
    // the ships' slots when a battery engages (never in a fall)
    const int SplashRing = 32;
    long[] splashMs = new long[64 * SplashRing];
    int[] splashShooter = new int[64 * SplashRing];
    float[] splashCal = new float[64 * SplashRing];
    Vec3d[] splashAt = new Vec3d[64 * SplashRing];
    float[] splashPeak = new float[64 * SplashRing];
    int[] splashHead = new int[64];
    bool ticking, ever;
    // each side's men's experience in thousandths (docs/battle/crew.md decision 6: whole numbers in commands, so a replay
    // is the same)
    readonly int[] experience = new int[256];
    bool experienceSet;
    // each side's fire discipline, an index of the settings' FireDisciplines (fire-control.md F6, decision 3)
    readonly byte[] disciplines = new byte[256];
    bool disciplinesSet;
    bool[] firing = new bool[64];
    int[] fireLoad = new int[64];

    // a tick's batteries (docs/battle/workers.md W6): each one's start in order on the sim thread (let go, its instruments
    // picked again: those reach its ship's other batteries), the rest of its second as a job (readings, the tracker, the
    // solution, the mounts ordered: its own state only), then in order the smoke cache's bookkeeping for its pair and its
    // salvo's event. Per battery due: whether it asked the smoke on its line (its depths, the work's time) and when its
    // salvo goes (-1 none); per worker, its mounts' firing flags.
    int[] due = new int[64];
    bool[] dueSmoked = new bool[64];
    long[] dueTicks = new long[64], dueWhen = new long[64];
    double[] dueTau = new double[64 * SmokeSystem.PairTaus];
    bool[][] firingOf = [new bool[64]];
    const int RunChunk = 4, RunMinBatteries = 16;

    readonly struct RunJob(GunnerySystem g, World world, long now) : IJob
    {
        public void Run(int from, int to, int worker)
        {
            for (int i = from; i < to; i++)
                g.Second(world, i, now, worker);
        }
    }

    internal GunnerySystem(SimSettings settings, ShipSystem ships, ShellSystem shells, CombatLog log, ulong seed)
    {
        (this.settings, this.ships, this.shells, this.log) = (settings, ships, shells, log);
        stream = RandomStream.Named(seed, "gunnery");
        salvos = new Salvo[Math.Max(settings.SalvoCapacity, 16)];
        freeSalvo = new Stack<int>(salvos.Length);   // as big as the pool, so freeing never grows it
        int engagements = Math.Max(16, 2 * settings.ShipCapacity);
        seaDue = new int[Math.Max(16, settings.ShipCapacity)];
        (fcs, sensors, pending, freeFc) = (new Fc[engagements], new Sensor[engagements * MaxSensors], new FcReading[engagements * PendingCap],
            new Stack<int>(engagements));
        layerMargin = new float[engagements * MaxLayers];
        Array.Fill(experience, (int)(Core.Experience.Trained * 1000));
    }

    // ------------------------------------------------------------------ commands

    /// <summary>A ship's battery (or all of them: -1) engages a ship (an entity). A level of -1 is the battery's own fit; a
    /// dev or a bench may try another preset's (0-8).</summary>
    public static Command Engage(int shooter, int battery, int target, int level = -1) =>
        new(Id, (ushort)GunneryCommand.Engage, shooter, A: target, B: battery, X: level);

    /// <summary>A ship's battery (or all: -1) ceases fire.</summary>
    public static Command Cease(int shooter, int battery = -1) => new(Id, (ushort)GunneryCommand.Cease, shooter, B: battery);

    /// <summary>A ship's battery (or all: -1) loads with a kind from now on, or (null) by the magazine order
    /// (docs/battle/shell-choice.md A2): the round in its guns goes first.</summary>
    public static Command Load(int shooter, int battery, ShellKind? kind) =>
        new(Id, (ushort)GunneryCommand.Load, shooter, A: kind is { } k ? (long)k + 1 : 0, B: battery);

    /// <summary>A side's men's experience, 0-3 (<see cref="Core.Experience"/>: the scenario's to give; trained by
    /// default), carried in thousandths.</summary>
    public static Command SetExperience(int side, double experience) =>
        new(Id, (ushort)GunneryCommand.SetExperience, side, A: (long)Math.Round(experience * 1000));

    /// <summary>A side's fire discipline, an index of <see cref="SimSettings.FireDisciplines"/> (the scenario's or the
    /// admiral's to give; the first by default).</summary>
    public static Command SetDiscipline(int side, int discipline) => new(Id, (ushort)GunneryCommand.SetDiscipline, side, A: discipline);

    public double Experience(int side) => experience[side & 255] / 1000.0;

    /// <summary>A ship's gun crews: her side's experience as far as she's worked up (docs/battle/crew.md decision 6).</summary>
    internal GunneryCrew CrewOf(in ShipState ship) => settings.GunneryCrewAt(settings.GunneryLevel(Experience(ship.Side), ship.WorkUp));

    public FireDiscipline Discipline(int side) => settings.FireDisciplines[disciplines[side & 255]];

    // filled as the captains ask, on the AI's workers (concurrent: a captain's miss mustn't tear another's read)
    readonly System.Collections.Concurrent.ConcurrentDictionary<(int Table, double Elevation), double> reach = new();

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
            reach.TryAdd((table, top), r = t.Fall(Math.Min(top, t.MaxElevationDeg), 10).Range);
        }
        return r;
    }

    /// <summary>A ship's battery's ranging cycle at a range, s (docs/battle/balance.md BL1ae): ranging, each salvo waits
    /// for the last one's fall to be spotted, its flight with the spot's delay and the wait's margin, or its guns' reload
    /// if that's longer; its lead shell's range table from a muzzle 10 m up (its reach's flight past it).</summary>
    public double RangingCycleS(Loadout lo, int battery, double rangeM)
    {
        var cls = lo.Class;
        double reload = 0;
        foreach (int m in cls.Links(cls.Batteries[battery].Mounts))
            reload = Math.Max(reload, cls.Mounts[m].ReloadS);
        var t = shells.Ballistics[shells.TableOf(lo, battery)];
        double e = t.ElevationFor(Math.Min(rangeM, ReachM(lo, battery)), 10);
        double tof = double.IsNaN(e) ? 0 : t.Fall(e, 10).Tof;
        return Math.Max(reload, tof + (settings.SpotDelayMs + settings.RangingWaitMarginMs) / 1000.0);
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
            case GunneryCommand.SetExperience:
                if (c.Entity is < 0 or > 255 || c.A is < 0 or > (long)(Core.Experience.Max * 1000))
                    throw new ArgumentException($"no experience {c.A / 1000.0} for side {c.Entity}");
                experience[c.Entity] = (int)c.A;
                experienceSet = true;
                break;
            case GunneryCommand.SetDiscipline:
                if (c.Entity is < 0 or > 255 || c.A < 0 || c.A >= settings.FireDisciplines.Length)
                    throw new ArgumentException($"no fire discipline {c.A} for side {c.Entity}");
                disciplines[c.Entity] = (byte)c.A;
                disciplinesSet = true;
                break;
            case GunneryCommand.Load:
                if (c.A < 0 || c.A > (long)ShellKind.He + 1)
                    throw new ArgumentException($"no shell kind {c.A - 1}");
                if (ships.Store.SlotOf(c.Entity) is int ls and >= 0)
                    for (int b = Math.Max(c.B, 0); b < (c.B < 0 ? ships.Store.Combat.Loadout(ls).Class.Batteries.Length : c.B + 1); b++)
                        ships.Store.Combat.Order(ls, b, c.A > 0 ? (ShellKind)(c.A - 1) : null);
                break;
        }
    }

    void EngageNow(World world, int shooter, int battery, int target, int level)
    {
        SplashRoom(ships.Store.Slots);
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
                FirstHitMs = -1, FirstStraddleMs = -1, EffectMs = -1, LastSign = -1, OverAt = double.NaN, ShortAt = double.NaN, DirectorLevel = lv, Fit = fit, DirectorFit = fit,
                Table = shells.TableOf(store.Combat.Loadout(slot), b), ElevationDeg = double.NaN, SetCourseDeg = double.NaN, SpoiledMs = -1,
            };
            fc.Bracket = FireControl.Bracket.Open(fit.Computer.FirstStepM);
            // the day's ballistics as the machine has them (docs/battle/error-sources.md ES3): its wind's measuring error,
            // its air's density and its guns' muzzle velocity, each off by its own error; the guns' and the day's, not the
            // target's: one draw a ship's battery (BL1ad); and what its spots learnt on its last target goes with it
            var rb = stream.Derive(0xB1A5u).Derive((ulong)(uint)shooter).Derive((ulong)b);
            (fc.MvZ, fc.DensityZ) = (rb.NextGaussian(), rb.NextGaussian());
            var own = store[slot];
            fc.WindZ = Earth.East(own.Position) * rb.NextGaussian() + Earth.North(own.Position) * rb.NextGaussian();
            BallisticDoubt(world, ref fc, cls, slot, Earth.Distance(own.Position, store[store.SlotOf(target)].Position));
            (fc.OwnHatP, fc.OwnHatV, fc.OwnHatHeadingDeg, fc.OwnHatSpeed, fc.OwnHatMs) = (own.Position, own.Forward * own.Speed, own.HeadingDeg, own.Speed, now);
            fc.SvMs = now;
            var carried = store.Combat.Carried(slot, b);
            (fc.CarryR, fc.CorrD, fc.ResidShare, fc.DeflDoubtRad) = carried.Known
                ? (carried.RangeShare, carried.LineRad, carried.RangeDoubtShare, carried.LineDoubtRad)
                : (0, 0, fc.BallR, fc.BallD);
            var r = stream.Derive((ulong)f).Derive((ulong)gen);
            (fc.GuessSpeedZ, fc.GuessCourseZ) = (r.NextGaussian(), r.NextGaussian());
            AddSensors(ref fc, f, cls, row, store.Combat.Condition(slot), now, ref r);
            store.Combat.Engage(slot, b, target);
            store.Combat.Began(slot, b, now);
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
            var s = new Sensor { Eye = -1, BearingMil = settings.EyeBearingMil, SeaPoint = -1 };
            foreach (int m in cls.Links(row.Mounts))
            {
                ref readonly var comp = ref cls.Components[cls.Mounts[m].Component];
                double h = comp.Bounds.Top - cls.Vertical.Waterline;
                bool w = cond.Lays(m, local: true);   // its layer can't see with its sights gone (E4)
                if (w && !works || w == works && h > best)
                    (best, works, s.X, s.Y, s.HeightM, s.SeaPoint) = (h, w, cls.Mounts[m].X, cls.Mounts[m].Y, h, SeaAboard.MountPoint(m));
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
            bool directed = false, frozen = false;
            foreach (int u in cls.Links(row.Units))
                if ((fc.Units & 1UL << u) != 0 && cls.FcUnits[u].Director is >= 0 and var d)
                {
                    bool dark = cond.DirectorDark(d, now);
                    (directed, frozen) = (directed || !dark, frozen || dark);
                }
            var fit = directed ? fc.DirectorFit : fc.DirectorFit with { Mount = row.Sights };
            // her power off (docs/battle/electrics.md EL5): a director's receivers freeze and the guns lay by their own sights,
            // a table with a motor goes to its hand cranks, slower; with no gyro her own course and the vertical go to hand too
            if ((frozen && !directed || cond.NoPower(now)) && fit.Computer.PowerKw > 0)
                fit = fit with { Computer = fit.Computer with { LatencyS = fit.Computer.LatencyS * settings.TableHandLatency } };
            if (cond.GyroOut(now))
                fit = fit with { Computer = fit.Computer with { HelmFree = false }, Mount = fit.Mount with { StableVertical = false } };
            if (fc.Local || fc.Fit != fit)
                (fc.Local, fc.Level, fc.Fit) = (false, fc.DirectorLevel, fit);   // a director back (E4: a timed outage over): under it again
        }
        fc.SensorCount = n;
        fc.SensorSet++;   // readings of the set before still in the computer's queue go in as they are
        fc.SpotHeightM = 0;
        var optics = fc.Fit.Optics;
        for (int k = 0; k < n; k++)
        {
            ref var s = ref sensors[f * MaxSensors + k];
            (s.Bias, s.RangeZ, s.BearingZ) = (r.NextGaussian(), r.NextGaussian(), r.NextGaussian());
            (s.Cuts, s.OffRangeM, s.OffBearingDeg) = (0, 0, 0);
            // put on her at once, its first reading now (the user, 2026-10-27: one at a random point of its cycle left a
            // layer by eye looking at a ship alongside for up to a minute); its own rate from there
            s.NextMs = now;
            fc.SpotHeightM = Math.Max(fc.SpotHeightM, s.HeightM);
        }
    }

    /// <summary>A sensor's readings a minute: an eye one, a rangefinder its optics', fewer past ×30 where the narrow field
    /// is slower to pick the target up (designer §3.1: ×√(30/M) [INFERRED]).</summary>
    /// <summary>The least share of the time an instrument holds her that it's read at (ES5): a reading every few minutes
    /// at worst, so one rolling hard is looked at again soon after she steadies.</summary>
    const double MinHeld = 0.05;

    static double Rate(in OpticsRow o, in Sensor s) => Ranges(o, s) ? o.ReadingsPerMin * Math.Sqrt(Math.Min(1, 30 / o.Magnification)) : 1;

    /// <summary>Whether a sensor takes ranges with its rangefinder: it has one, and the fit's optics can read it (the
    /// eyeball's can't: a director's rangefinder under it ranges by eye).</summary>
    static bool Ranges(in OpticsRow o, in Sensor s) => s.BaseM > 0 && o.Reads;

    /// <summary>A mount's own layer's margin on the target from his sight, on its gunhouse's roof (as local control takes
    /// him: fire-control.md F4b), by eye through his telescope (FL5): he fires while it's 1 or more, and lays by it (ES6).</summary>
    double LayerMargin(ShipClass cls, int mount, in OpticsRow optics, in ShipState ship, in ShipState target, in Air air, ReadOnlySpan<double> smoke,
        int slot, in ShipFrame frame, long now)
    {
        ref readonly var m = ref cls.Mounts[mount];
        var s = new Sensor { Eye = -1, X = m.X, Y = m.Y, HeightM = LayerHeightM(cls, mount), BearingMil = settings.EyeBearingMil, SeaPoint = SeaAboard.MountPoint(mount) };
        return MarginOf(cls, s, optics, frame.ToWorld(s.X, s.Y, 0), ship.HeadingDeg, target, air, smoke, slot, frame, now);
    }

    /// <summary>The smoke on one of a ship's lines to a target now over the field's mean (docs/battle/error-sources.md ES6):
    /// its flicker (<see cref="SmokeModel.Flicker"/>), one state a window of <see cref="SimSettings.SmokeFlickerS"/> for
    /// each eye (or a layer's sight, by his mount's point), drawn from the world's stream by the pair, the eye and the
    /// window, so it's the same at any compression; times the ladder's smoke rung.</summary>
    double SmokeNow(int slot, int targetSlot, int eye, int seaPoint, long now)
    {
        var store = ships.Store;
        long window = now / Math.Max((long)(settings.SmokeFlickerS * 1000), 1);
        int code = eye >= 0 ? eye : 0x1000 + seaPoint;
        double z = stream.Derive(0x5307u).Derive((ulong)(uint)store[slot].Entity).Derive((ulong)(uint)store[targetSlot].Entity).Derive((ulong)code)
            .Derive((ulong)window).NextGaussian();
        return settings.GunneryErrors.Smoke * SmokeModel.Flicker(z, settings.SmokeFlickerLn);
    }

    /// <summary>The battery's machine's doubt of its ballistics (ES3) at a range, from its first mount's muzzle; kept as it
    /// was where the range is out of the guns' reach.</summary>
    void BallisticDoubt(World world, ref Fc fc, ShipClass cls, int slot, double rangeM)
    {
        var store = ships.Store;
        var mounts = cls.Links(cls.Batteries[fc.Battery].Mounts);
        if (mounts.Length == 0)
            return;
        var table = shells.Ballistics[fc.Table];
        double h0 = Muzzle(cls, store[slot].Frame, mounts[0]).Length - Earth.Radius, e = table.ElevationFor(rangeM, h0);
        if (double.IsNaN(e))
            e = table.MaxElevationDeg - 2;
        var wx = world.Weather.At(store[slot].Position, world.Time);
        (fc.BallR, fc.BallD) = BallisticResidual.Doubt(fc.Fit.Computer, BallisticResidual.Of(table, e, h0), wx.WindMs, rangeM);
    }

    /// <summary>A mount's layer's eye over the water, m: on its gunhouse's roof.</summary>
    static double LayerHeightM(ShipClass cls, int mount) => cls.Components[cls.Mounts[mount].Component].Bounds.Top - cls.Vertical.Waterline;

    /// <summary>Whether any column may stand about a ship now (ES2): a miss in her ring within the longest a column
    /// stands (a 20" shell's at full height, under 9 s).</summary>
    bool Standing(int tslot, long now)
    {
        if (tslot < 0 || tslot >= splashHead.Length || settings.GunneryErrors.Masking <= 0)
            return false;
        for (int k = tslot * SplashRing, end = k + SplashRing; k < end; k++)
            if (splashPeak[k] > 0 && now - splashMs[k] < SplashColumn.WithinMs)
                return true;
        return false;
    }

    /// <summary>Whether the columns of the falls aimed at her (her ring) hide her from an eye (<see cref="SplashColumn.Hides"/>),
    /// their size scaled by <see cref="GunneryErrorScale.Masking"/>.</summary>
    bool Masked(Vec3d at, double eyeM, int tslot, in ShipState target, long now)
    {
        var tc = target.Handling.Class;
        int k = tslot * SplashRing;
        return SplashColumn.Hides(at, eyeM, target.Position, target.Forward, tc.Motion.LengthM, tc.Motion.BeamM, tc.Silhouette.TopM,
            splashAt.AsSpan(k, SplashRing), splashPeak.AsSpan(k, SplashRing), splashCal.AsSpan(k, SplashRing), splashMs.AsSpan(k, SplashRing),
            now, settings.GunneryErrors.Masking);
    }

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
            Array.Resize(ref layerMargin, fcs.Length * MaxLayers);
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
        {
            if (fc.Phase == FirePhase.Effect && fc.RangeM > 0)
                ships.Store.Combat.Carry(slot, fc.Battery, Learnt(f));
            ships.Store.Combat.Engage(slot, fc.Battery, 0);
        }
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

    /// <summary>The fire-control tick's grid, ms: <see cref="SimSettings.FireControlMs"/>, the coarse tier's
    /// <see cref="SimSettings.FireControlCoarseMs"/> (docs/battle/coarse.md J7: the same chain at a coarser cadence).</summary>
    int GridMs(World world) => world.Coarse ? settings.FireControlCoarseMs : settings.FireControlMs;

    /// <summary>The fire-control tick: each engaged battery measured, tracked, its mounts ordered and its next salvo
    /// timed; the next tick on the grid while any battery is engaged. From a command (<paramref name="schedule"/>), only
    /// the next tick is scheduled.</summary>
    void Tick(World world, bool schedule)
    {
        long now = world.Time.Ms, step = GridMs(world);
        if (!schedule)
        {
            int n = 0;
            for (int f = 0; f < highFc; f++)
                if (fcs[f].Alive && Start(world, f, now))
                {
                    if (n == due.Length)
                    {
                        Array.Resize(ref due, 2 * n);
                        Array.Resize(ref dueSmoked, 2 * n);
                        Array.Resize(ref dueTicks, 2 * n);
                        Array.Resize(ref dueWhen, 2 * n);
                        Array.Resize(ref dueTau, 2 * n * SmokeSystem.PairTaus);
                    }
                    due[n++] = f;
                }
            if (firingOf.Length < world.Workers.Participants)
            {
                int had = firingOf.Length;
                Array.Resize(ref firingOf, world.Workers.Participants);
                for (int w = had; w < firingOf.Length; w++)
                    firingOf[w] = new bool[64];
            }
            world.Smoke.PrepareQueries(world.Workers.Participants);
            Seaways(world, n);
            world.Workers.For(new RunJob(this, world, now), n, RunChunk, RunMinBatteries);
            var store = ships.Store;
            for (int i = 0; i < n; i++)
            {
                ref readonly var fc = ref fcs[due[i]];
                if (dueSmoked[i])
                    world.Smoke.Answered(world, store.SlotOf(fc.Shooter), store.SlotOf(fc.Target),
                        dueTau.AsSpan(i * SmokeSystem.PairTaus, SmokeSystem.PairTaus), dueTicks[i]);
                if (dueWhen[i] >= 0)
                    world.Schedule(new WorldTime(dueWhen[i]), EventTier.Fine, due[i], Id, SalvoEvent, fc.Generation, fc.Arm);
            }
        }
        bool any = false;
        for (int f = 0; f < highFc; f++)
            any |= fcs[f].Alive;
        if (any && !ticking)
        {
            long next = schedule ? (now + step - 1) / step * step : (now / step + 1) * step;
            world.Schedule(new WorldTime(next), EventTier.Fine, 0, Id, TickEvent);
            ticking = true;
        }
    }

    /// <summary>One battery's second begun, on the sim thread in battery order: let go if its ship or target is gone,
    /// its instruments picked again after damage or a unit let go (what reaches its ship's other batteries). Whether the
    /// rest of it (<see cref="Second"/>) runs. Any clock tier: a coarse ship's pose is its segment's at the second, as a
    /// fine one's is its tick's (docs/battle/coarse.md J2).</summary>
    bool Start(World world, int f, long now)
    {
        ref var fc = ref fcs[f];
        var store = ships.Store;
        int slot = store.SlotOf(fc.Shooter), tslot = store.SlotOf(fc.Target);
        if (slot < 0 || tslot < 0)
        {
            Free(f);
            return false;
        }
        var ship = store[slot];
        var cls = ship.Handling.Class;
        var cond = store.Combat.Condition(slot);
        if (cond.Version != fc.Condition || fc.Repick)
        {
            // damage: its instruments picked again from what still works; or a unit it was short of let go
            var rs = cond.Version != fc.Condition
                ? stream.Derive((ulong)f).Derive((ulong)fc.Generation).Derive(0xDA3u).Derive((ulong)cond.Version)
                : stream.Derive((ulong)f).Derive((ulong)fc.Generation).Derive(0xDA4u).Derive((ulong)now);
            AddSensors(ref fc, f, cls, cls.Batteries[fc.Battery], cond, now, ref rs);
        }
        return true;
    }

    /// <summary>The rest of due battery i's second, on a worker: readings taken and taken in, the solution, the mounts
    /// ordered, the salvo timed (its event scheduled after, in battery order). Its own state only.</summary>
    void Second(World world, int i, long now, int worker)
    {
        int f = due[i];
        (dueSmoked[i], dueTicks[i], dueWhen[i]) = (false, 0, -1);
        ref var fc = ref fcs[f];
        var store = ships.Store;
        int slot = store.SlotOf(fc.Shooter), tslot = store.SlotOf(fc.Target);
        var ship = store[slot];
        var cls = ship.Handling.Class;
        var target = store[tslot];
        var cond = store.Combat.Condition(slot);
        var computer = fc.Fit.Computer;
        var optics = fc.Fit.Optics;
        var crew = CrewOf(ship);
        var pic = world.Sightings.Of(ship.Side);
        int ci = pic?.IndexOf(fc.Target) ?? -1;
        bool inSight = ci >= 0 && pic![ci].InSight;
        if (!fc.Tracking && !inSight)
        {
            fc.State = BatteryState.NoContact;
            store.Combat.SetSees(slot, fc.Battery, false);
            return;
        }
        var ownV = ship.Forward * ship.Speed;
        Reckon(ref fc, ship, now);
        Vertical(ref fc, f, ship, now);
        RollOf(world, ref fc, ship, cls, cond);   // her roll and pitch now: her instruments' holding (ES5) and her layers
        bool inputs = computer.HelmFree;   // a machine that takes her motion in reads and orders from its own ship (FL1)

        // measure: each sensor that's due and makes the target out through its optics
        var frame = ship.Frame;
        var tpos = target.Position;
        double combat = settings.GunneryCombat;
        ref readonly var row = ref cls.Batteries[fc.Battery];
        var mounts = cls.Links(row.Mounts);
        var table = shells.Ballistics[fc.Table];
        var muzzle = Muzzle(cls, frame, mounts[0]);
        // point blank, a reading goes to the layers at once: they lay on what they see, with no plot to wait for (the user,
        // 2026-10-27: "it shouldn't take 20 s to figure out someone is 500 m from you"); further out, through the computer
        long latencyMs = inSight && PointBlankAt(fc, f, table, muzzle.Length - Earth.Radius, Earth.Distance(ship.Position, tpos), target, crew,
            out _, out _, out _) == true ? 0 : (long)(computer.LatencyS * 1000);
        Air air = default;
        bool sees = false, aired = false;
        double bestMargin = 0;
        var smoke = dueTau.AsSpan(i * SmokeSystem.PairTaus, SmokeSystem.PairTaus);
        bool columns = inSight && Standing(tslot, now);
        for (int k = 0; k < fc.SensorCount; k++)
        {
            ref var s = ref sensors[f * MaxSensors + k];
            bool due = now >= s.NextMs;
            if (!due && (sees || !inSight))
                continue;   // one that sees is enough to fire on; the margins are looked at for readings
            if (!aired)
                (air, aired) = (AirAt(world, ship.Position, slot, inSight ? tslot : -1, smoke, i, worker), true);
            var at = frame.ToWorld(s.X, s.Y, 0);
            double margin = inSight ? MarginOf(cls, s, optics, at, ship.HeadingDeg, target, air, smoke, slot, frame, now) : 0;
            // her bow's spray or green water on its glass: nothing seen through it till it's cleared (ES4)
            bool seen = margin >= 1 && !GlassWet(world, fc.Shooter, slot, ship, cls, cond, s.SeaPoint, s.HeightM + cls.Vertical.Waterline, now);
            sees |= seen;
            bestMargin = Math.Max(bestMargin, margin);
            if (!due)
                continue;
            var r = stream.Derive((ulong)f).Derive((ulong)fc.Generation).Derive((ulong)now).Derive((ulong)k);
            // short-handed, slower (crew.md N2); an unstabilised instrument only while its man holds her in its field as
            // she rolls and pitches (ES5)
            double held = Ranges(optics, s)
                ? FireControl.Held(optics, fc.Roll.RateSigmaRadS, fc.Pitch.RateSigmaRadS, Earth.Bearing(at, tpos) - ship.HeadingDeg,
                    settings.InstrumentFollowS, settings.InstrumentFieldDeg, settings.GunneryErrors.Seaway)
                : 1;
            s.NextMs = now + (long)(60_000 / (Rate(optics, s) * Math.Max(held, MinHeld) * (s.Eye >= 0 ? cond.EyeCrew[s.Eye] : 1)) * r.NextDouble(0.7, 1.3));
            if (!seen || columns && Masked(at, s.HeightM, tslot, target, now))
                continue;   // nothing to read; or she stands behind the shorts' columns, no cut till they fall (ES2)
            double range = Earth.Distance(at, tpos), brg = Earth.Bearing(at, tpos), sr, bias, sb;
            double st = Steadiness.Stress(cls, cond, s.Eye >= 0 ? cls.Eyes[s.Eye].Component : cls.Mounts[mounts[0]].Component, now, settings);   // BL1d
            st *= Vibration(cls, ship, s.Support, s.X);   // BL1h: the ship's own shaking at speed
            if (Ranges(optics, s))
            {
                // its unit of error, worse as the air takes the target's contrast toward the eye's threshold (F5c)
                double kAir = SightModel.RangingFactor(margin, optics, settings);
                double unit = kAir * FireControl.RangefinderUnitM(s.BaseM, optics, FireControl.Magnification(optics, s.Support, s.HeightM), range);
                (sr, bias, sb) = (crew.Operator * combat * st * unit, s.Bias * settings.RangefinderBiasUnits * unit, s.BearingMil * Math.Sqrt(kAir) * FireControl.Mil);
            }
            else
                (sr, bias, sb) = (settings.EyeRangeShare * range * crew.Operator * combat * st, s.Bias * settings.EyeRangeBiasShare * range,
                    s.BearingMil * FireControl.Mil);
            // the same man reads the same ship all through and misjudges her the same way, through a rangefinder or by eye:
            // his error drawn once as he was put on her, scaled by what he reads through now (the range, the air, his
            // stress); never a fresh draw each reading, nor a wander (the user, 2026-11-02: each moved the guns)
            double ek = settings.GunneryErrors.Readings;
            double zr = Math.Max(range + ek * bias + ek * sr * s.RangeZ, 100), zb = brg + double.RadiansToDegrees(ek * sb * s.BearingZ);
            // a machine fed her motion has the instrument where it reckons her, and the bearing off her head (the director's
            // train, at once) made true by the course it has (a hand-followed course late: FL1)
            var atC = inputs ? Reckoned(fc, ship, at) : at;
            double zbC = inputs ? zb + Wrap(fc.OwnHatHeadingDeg - ship.HeadingDeg) : zb;
            var fromC = inputs ? fc.OwnHatP : ship.Position;
            var (z, r11, r12, r22) = Track.Reading(atC, zr, zbC, sr, sb);
            if (fc.PendCount == PendingCap)
                PopReading(ref fc, f, target, now, ship.Position, ownV);   // the computer's queue is full: the oldest goes in now
            int at2 = f * PendingCap + (fc.PendHead + fc.PendCount++) % PendingCap;
            // the computer takes every instrument's cut from one reference point, the ship's (the tables' parallax
            // correction: a director forward and one aft see the target on bearings tens of mils apart close in)
            pending[at2] = new FcReading
            {
                Sensor = k, SensorSet = fc.SensorSet, Ms = now, ArriveMs = now + latencyMs, From = fromC, OwnVelocity = inputs ? fc.OwnHatV : ownV, Z = z,
                RangeM = Earth.Distance(fromC, z), BearingDeg = Earth.Bearing(fromC, z), SigmaRangeM = sr, SigmaBearingRad = sb, R11 = r11,
                R12 = r12, R22 = r22,
            };
        }

        if (bestMargin > 0 || !inSight)
            fc.Margin = bestMargin;   // a second whose sensors weren't looked at keeps the last
        // the computer: readings that have come through
        while (fc.PendCount > 0 && pending[f * PendingCap + fc.PendHead].ArriveMs <= now)
            PopReading(ref fc, f, target, now, ship.Position, ownV);
        if (!fc.Tracking)
        {
            fc.State = BatteryState.Tracking;   // its first reading still on its way
            if (sees)
            {
                // the turrets train on her as she's seen (the director's train, or each trainer on his own sight), not
                // when the plot has its first range (docs/battle/fc-ladder-check.md FL2): their elevation waits for it
                double seen = Mod(Earth.Bearing(ship.Position, tpos) - ship.HeadingDeg);
                for (int j = 0; j < mounts.Length; j++)
                    if (cond.MountWorks[mounts[j]] && !store.Combat.Illuminating(slot, mounts[j]))
                        store.Combat.Order(slot, mounts[j], cls.Mounts[mounts[j]], now, seen,
                            CombatStore.ElevationAt(store.Combat.Mount(slot, mounts[j]), cls.Mounts[mounts[j]], now));
            }
            return;
        }
        if (computer.Tracker == TrackerKind.Judgement && sees && now >= fc.JudgeMs)
        {
            // a sailor looks at her again: a fresh guess at her course and speed
            fc.Tracker.Rejudge(Guess(computer, fc, target.Position, target.Forward * target.Speed));
            fc.JudgeMs = now + (long)(computer.RejudgeS * 1000);
        }

        // solve, and order the mounts
        fc.Sees = sees;
        store.Combat.SetSees(slot, fc.Battery, sees);
        var aim = Aim(ref fc, f, table, muzzle, now, ship.Position, ownV, ship.HeadingDeg, target, crew);
        bool laid = FireControl.Solve(table, muzzle, ship.Forward * ship.Speed, aim, out var lay, fc.ElevationDeg);
        double bearing = Mod((laid ? Earth.Heading(muzzle, lay.GunHorizontal) : Earth.Bearing(ship.Position, aim)) - ship.HeadingDeg);
        double elevation = laid ? lay.GunElevationDeg : double.PositiveInfinity;
        (fc.OrderBearingDeg, fc.OrderElevationDeg) = (bearing, elevation);
        if (firingOf[worker].Length < mounts.Length)
            firingOf[worker] = new bool[Math.Max(mounts.Length, firingOf[worker].Length * 2)];
        var firing = firingOf[worker];
        bool bears = false;
        var lm = layerMargin.AsSpan(f * MaxLayers, MaxLayers);
        // a gun laid by its own layer fires only while he sees her from his sight (docs/battle/fc-ladder-check.md FL5): on
        // the gunhouse's roof, through the smoke there, her own funnels' over her guns as much as the enemy's (the 1912
        // trial: Orion's layers lost her in it where Thunderer's director aloft didn't); a director's guns follow its
        // layer. In clear air a sight lower down sees what the top does
        bool ownSight = !fc.Fit.Mount.DirectorFiring && sees;
        if (ownSight && !aired)
            (air, aired) = (AirAt(world, ship.Position, slot, tslot, smoke, i, worker), true);
        bool ownColumns = ownSight && columns;   // nor while the shorts' columns hide her from his sight (ES2)
        ownSight &= air.Smoked;
        for (int j = 0; j < mounts.Length; j++)
        {
            ref readonly var mr = ref cls.Mounts[mounts[j]];
            // one out of action or jammed stays where it stopped; one lighting her with star shell is laid for that (Q5)
            bool works = cond.MountWorks[mounts[j]] && !store.Combat.Illuminating(slot, mounts[j]);
            bool inTraverse = works && store.Combat.Order(slot, mounts[j], mr, now, bearing, elevation);
            firing[j] = inTraverse && cond.Lays(mounts[j], fc.Local) && CombatStore.InArcs(cls, mr, bearing) &&
                        store.Combat.Mount(slot, mounts[j]).Rounds > 0 && elevation <= mr.MaxElevationDeg &&
                        !GunWet(fc.Shooter, slot, ship, cls, cond, mounts[j], now);
            double layer = double.NaN;
            if (firing[j] && ownSight)
                firing[j] = (layer = LayerMargin(cls, mounts[j], optics, ship, target, air, smoke, slot, frame, now)) >= 1;
            if (j < MaxLayers)
                lm[j] = (float)layer;
            if (firing[j] && !fc.Fit.Mount.DirectorFiring)   // his sight on the gunhouse's roof blinded by the sea (ES4)
                firing[j] = !GlassWet(world, fc.Shooter, slot, ship, cls, cond, SeaAboard.MountPoint(mounts[j]), cls.Components[mr.Component].Bounds.Top, now);
            if (firing[j] && ownColumns)
                firing[j] = !Masked(frame.ToWorld(mr.X, mr.Y, 0), LayerHeightM(cls, mounts[j]), tslot, target, now);
            bears |= firing[j];
        }
        // fire discipline (F6): the bracket widens at the computer's doubt of the range rate along the line of fire; the
        // target seen to turn, or a plain computer's own course gone stale, sends a battery firing for effect back to
        // ranging, its bracket opened to the first step. A plain computer's Dumaresq has her course set on it by hand: it's
        // put out once she has turned a few degrees from that course (docs/battle/balance.md BL1o: the Mk IV kept the
        // enemy's relative direction "within a few degrees" in a turn), its new course set then. A machine that takes her
        // motion in isn't put out by her turns at all (balance.md BL1t): its solution follows her, and a turn costs what the
        // own-turn kick costs each salvo fired in it
        var along = Earth.Forward(aim, Earth.Bearing(ship.Position, aim));
        (fc.DriftMs, fc.PosSigmaM, fc.RangeM) = (fc.Tracker.RateSigma(computer, along), fc.Tracker.PositionSigma(along), Earth.Distance(ship.Position, aim));
        if (fc.Phase == FirePhase.Opening && now < fc.OpenMs && (fc.PointBlank || Settled(fc)))
            fc.OpenMs = now;   // the solution good enough to range from (or none needed): no more waiting
        fc.State = !laid ? BatteryState.OutOfRange : !bears ? BatteryState.NotBearing : now < fc.OpenMs ? BatteryState.Tracking : BatteryState.Firing;
        bool plain = !computer.HelmFree && computer.Tracker is TrackerKind.RateClock or TrackerKind.Judgement, put = false;
        if (plain)
        {
            double off = double.IsNaN(fc.SetCourseDeg) ? 0 : Mod(ship.HeadingDeg - fc.SetCourseDeg);
            put = Math.Min(off, 360 - off) > settings.OwnCourseStaleDeg;
            if (put || double.IsNaN(fc.SetCourseDeg))
                fc.SetCourseDeg = ship.HeadingDeg;
        }
        if (put || fc.Tracker.Turns != fc.Turns)
            Disturbed(ref fc, now);
        fc.Turns = fc.Tracker.Turns;
        if (fc.State == BatteryState.Firing && fc.Phase == FirePhase.Opening)
            (fc.Phase, fc.Bracket) = (FirePhase.Ranging, FireControl.Bracket.Open(FirstStep(fc), fc.CarryR * fc.RangeM));
        // the sea under her (BL1b): her layers follow the roll with their gear when it lays faster than she rolls (continuous
        // aim), or a stable vertical gives them the level; else they fire as the roll brings the sight on (a director's
        // layer always, at the key)
        double peakRoll = 2 * double.RadiansToDegrees(fc.Roll.RateSigmaRadS);
        fc.OnRoll = !fc.Fit.Mount.StableVertical && fc.Roll.AmplitudeRad > 0 &&
                    (fc.Fit.Mount.DirectorFiring || cls.Mounts[mounts[0]].ElevateDegS < settings.ContinuousAimMargin * peakRoll);

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
        {
            store.Combat.SetFiresForEffect(slot, fc.Battery, fc.State == BatteryState.Firing && fc.Phase == FirePhase.Effect);
            return;
        }
        // the gunnery officer's worth of the salvo (BL1p): while her turn spoils it he holds it for a steadier moment, up to
        // a reload's worth; a shell worth little from here, one salvo at a time; worth nothing, held. Point blank his layers
        // fire as fast as they lay
        int guns = 0;
        for (int j = 0; j < mounts.Length; j++)
            guns += firing[j] ? cond.Guns(mounts[j]) : 0;
        fc.Pared = false;
        if (!fc.PointBlank && guns > 0)
        {
            var (pNow, pSteady, pCould) = Worth(fc, cls, ship, frame, store[tslot], row, mounts, guns, crew, now);
            double floor = settings.FireWorthFloorPct / 100 / Math.Max(RoundsShare(slot, cls, mounts), settings.FireWorthLowRounds);
            if (pCould < settings.FireWorthHoldShare * floor)
            {
                fc.State = BatteryState.Holding;
                store.Combat.SetFiresForEffect(slot, fc.Battery, false);
                return;
            }
            bool spoiled = pNow < settings.FireWorthSteadyShare * pSteady, poor = pCould < floor;
            fc.Pared = spoiled || poor;
            fc.SpoiledMs = !spoiled ? -1 : fc.SpoiledMs < 0 ? now : fc.SpoiledMs;
            store.Combat.SetFiresForEffect(slot, fc.Battery, fc.Phase == FirePhase.Effect);   // paced or not: her captain holds for it
            if (spoiled && now - fc.SpoiledMs < 1000 * settings.FireWorthWaitReloads * cls.Mounts[mounts[0]].ReloadS)
                return;   // for her turn to ease
            if (poor && fc.InFlight > 0)
                return;   // one salvo at a time: the next waits on the last's fall
        }
        else
            store.Combat.SetFiresForEffect(slot, fc.Battery, fc.Phase == FirePhase.Effect);
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
        if (fc.OnRoll)
        {
            // fired on the roll (research §4.4): once ready, the layer waits for it to bring his sight on, once a roll, unless
            // the roll stays within his own laying error (the share of a roll his sight is on her anyway); drawn once for the
            // salvo due, from when it's ready
            if (fc.RollMs < when)
            {
                var rr = stream.Derive((ulong)f).Derive((ulong)fc.Generation).Derive(0x5E1u).Derive((ulong)fc.Salvos);
                double on = 2 / Math.PI * Math.Asin(Math.Min(1, fc.Fit.Mount.LayMil * crew.Lay * FireControl.Mil / fc.Roll.AmplitudeRad));
                fc.RollMs = rr.NextDouble() < on ? when : when + (long)(rr.NextDouble() * fc.Roll.PeriodS * 1000);
            }
            when = Math.Max(when, fc.RollMs);
        }
        if (when < now + GridMs(world))
        {
            fc.SalvoPending = true;
            fc.Arm++;
            dueWhen[i] = when;   // scheduled after the job, in battery order
        }
    }

    /// <summary>A battery's ship's roll now (docs/battle/balance.md BL1b): the world's sea at her heading and speed, her
    /// natural period stretched as she loses GM (T ∝ 1/√GM: a ship that has taken water rolls slower and heavier), worked
    /// out again only when the sea, her heading (2°), speed (0.25 m/s) or GM (1%) moves a step, and then from the steps'
    /// values, so it's the same whenever it's asked (any compression).</summary>
    void RollOf(World world, ref Fc fc, in ShipState ship, ShipClass cls, ShipCondition cond)
    {
        double gm0 = Math.Max(cls.Hydrostatics.GmT, 1e-6);
        int h = (int)(Mod(ship.HeadingDeg) / 2) % 180, v = (int)Math.Clamp(ship.Speed / 0.25, 0, 4095), g = (int)Math.Clamp(Math.Round(100 * cond.GmM / gm0), 5, 4095);
        long key = ((long)world.Sea.Id << 40) | ((long)h << 24) | ((long)v << 12) | (uint)g;
        if (key == fc.RollKey)
            return;
        fc.RollKey = key;
        var m = cls.Motion;
        fc.Roll = world.Sea.At(h * 2 + 1, (v + 0.5) * 0.25, m.LengthM, m.BeamM, m.RollPeriodS * Math.Sqrt(100.0 / g), m.RollDamping, settings.RollExcitation);
        fc.Pitch = world.Sea.Pitch(h * 2 + 1, (v + 0.5) * 0.25, m.LengthM, m.BeamM, settings.PitchPeriodK * Math.Sqrt(Math.Max(m.DraughtM, 0.5)), settings.PitchDamping);
    }

    /// <summary>Vibration at speed on an instrument or a gun's sights (docs/battle/balance.md BL1h; research §1.1): the
    /// multiplier 1 + a (v / v_max)² on its random error, a by what it stands on, toward 1 on a light, powerful hull, more in
    /// her after third.</summary>
    double Vibration(ShipClass cls, in ShipState ship, InstrumentSupport support, double x)
    {
        var m = cls.Motion;
        double v = ship.Speed / Math.Max(m.MaxSpeedKn * Handling.MsPerKnot, 0.1);
        double a = support switch { InstrumentSupport.Pole => 1.0, InstrumentSupport.Tripod or InstrumentSupport.Cage or InstrumentSupport.Lattice => 0.6, _ => 0.3 };
        double light = Math.Clamp((m.PowerShp / Math.Max(m.FullDisplacementT, 1) - settings.VibrationLightShpT) / settings.VibrationLightSpanShpT, 0, 1);
        a += (1 - a) * light;
        a *= 1 + (settings.VibrationAft - 1) * Math.Clamp((-x / (0.5 * m.LengthM) - 1 / 3.0) / (2 / 3.0), 0, 1);
        return 1 + a * v * v;
    }

    /// <summary>The air at a place now: the visibility, the night's light (docs/battle/night.md Q1: the sky's luminance,
    /// the moon's bearing and its silhouette's strength), and the sun's bearing when it's low enough to silhouette a ship
    /// against the horizon (else NaN), as the sightings read them; whether there's smoke on the lines to the target
    /// (docs/battle/smoke.md Z3); the lights that may light her (docs/battle/night.md Q2), and what her own guns' last
    /// flash leaves the firing ship's eyes adapted to (cd/m²).</summary>
    readonly record struct Air(double VisibilityM, NightSky Sky, double SunBearingDeg, bool Smoked, Lights Lights, double DazzleCdM2);

    /// <summary><see cref="AirAt(World, Vec3d, int, int, Span{double})"/> for due battery i on a worker: the smoke from
    /// the cache or worked out on the worker's scratch, the cache's bookkeeping after the job.</summary>
    Air AirAt(World world, Vec3d at, int slot, int tslot, Span<double> smoke, int i, int worker)
    {
        bool smoked = world.Smoke.On && tslot >= 0;
        dueSmoked[i] = smoked;
        if (smoked && !world.Smoke.Cached(world, slot, tslot, smoke))
        {
            long p0 = Probes.Now;
            world.Smoke.WorkOut(world, slot, tslot, smoke, worker);
            dueTicks[i] = Probes.Now - p0;
        }
        return AirOf(world, at, slot, smoked);
    }

    /// <summary>The air at a ship (slot) now, and the smoke on her lines to the target (slot; none for -1) into
    /// <paramref name="smoke"/> when the field is on: the looks' pair, so mostly from its cache.</summary>
    Air AirAt(World world, Vec3d at, int slot, int tslot, Span<double> smoke)
    {
        bool smoked = world.Smoke.On && tslot >= 0;
        if (smoked)
            world.Smoke.Tau(world, slot, tslot, smoke);
        return AirOf(world, at, slot, smoked);
    }

    Air AirOf(World world, Vec3d at, int slot, bool smoked)
    {
        double el = Light.SunElevationDeg(at, world.Time);
        bool low = el >= settings.SightSilhouetteLowDeg && el <= settings.SightSilhouetteHighDeg;
        var weather = world.Weather.At(at, world.Time);
        var (cd, ms) = world.Ships.Store.Combat.LastFlash(slot);
        return new Air(weather.VisibilityM, NightLight.At(at, world.Time, weather.Cloud, settings), low ? Light.SunBearingDeg(at, world.Time) : double.NaN,
            smoked, world.Lights, SightModel.DazzleCdM2(cd, (world.Time.Ms - ms) / 1000.0, settings));
    }

    /// <summary>A battery's sensor's margin on the target (fire-control.md F5c: the sighting model's, through its optics'
    /// magnification, or a layer's or an eye's binoculars): 0 when its eye doesn't bear (a blind sector) or the target is
    /// under the horizon; under 1 it can't make the target out, so takes no reading and doesn't see the fall. Through the
    /// smoke on its line (docs/battle/smoke.md Z3) when there is any, the pair's answer read at its height. A searchlight's
    /// beam on her lights her, her own ship's beams veil the sensor's view down them, others' glare in it, and her own
    /// lamp in its view is a point to aim at (docs/battle/night.md Q4).</summary>
    double MarginOf(ShipClass cls, in Sensor s, in OpticsRow optics, Vec3d at, double headingDeg, in ShipState target, in Air air,
        ReadOnlySpan<double> smoke, int slot, in ShipFrame frame, long now)
    {
        var tc = target.Handling.Class;
        var tp = target.Position;
        double d = Earth.Distance(at, tp);
        if (d > SightModel.HorizonM(s.HeightM, settings) + SightModel.HorizonM(tc.Silhouette.TopM, settings))
            return 0;
        double brg = Earth.Bearing(at, tp);
        if (s.Eye >= 0 && !cls.Eyes[s.Eye].Sees(brg - headingDeg))
            return 0;
        double sil = !double.IsNaN(air.SunBearingDeg) && Math.Abs(Wrap(brg - air.SunBearingDeg)) <= settings.SightSilhouetteArcDeg ? 1 : 0;
        bool ranges = Ranges(optics, s);
        double pupil = SightModel.PupilMm(air.Sky.SkyCdM2), back = Earth.Bearing(tp, at);
        var (lux, from) = air.Lights.On(target.Slot, tp, air.VisibilityM);
        var line = new SightLine(d, back - target.HeadingDeg, air.VisibilityM, air.Sky.Toward(brg), sil,
            air.Smoked ? SmokeModel.EyeTau(smoke, cls.Eyes, s.X, s.Y, s.HeightM).Scaled(SmokeNow(slot, target.Slot, s.Eye, s.SeaPoint, now)) : default,
            Math.Abs(target.Speed),
            ranges ? SightModel.TelescopeGlass(optics, pupil) : SightModel.LookoutGlass(pupil, settings),
            lux > 0 ? lux * Lights.Facing(from, back) : 0, air.DazzleCdM2);
        double lamp = 0, lampM = 0;
        if (air.Lights.Flares > 0)
            line = Sightings.Backlit(line, air.Lights.BacklightCdM2(at, tp, air.VisibilityM, settings));   // a flare beyond her (Q5)
        if (air.Lights.Beams > 0 || air.Lights.Flares > 0)
            line = line with { VeilCdM2 = air.Lights.VeilCdM2(slot, frame.ToWorld(s.X, s.Y, s.HeightM), tp, air.VisibilityM, settings, line.SkyCdM2) };
        if (air.Lights.Beams > 0)
            (lamp, _, lampM) = air.Lights.LampsOf(target.Slot, at, air.VisibilityM, settings);
        double mag = ranges ? optics.Magnification : settings.SightLookoutMagnification;
        // her bow wave gives a point of aim in the dark (docs/battle/night.md Q1), as her lamp does (Q4)
        double margin = Math.Max(SightModel.Margin(s.HeightM, mag, tc, line, settings), SightModel.BowWaveMargin(s.HeightM, mag, tc, line, settings));
        return lamp > 0
            ? Math.Max(margin, SightModel.LightMargin(s.HeightM, lamp, lampM, d, air.VisibilityM, line.SkyCdM2 + line.VeilCdM2, settings,
                air.Smoked ? SightModel.SmokeAt(line.Smoke, lampM, tc.Silhouette.TopM, settings) : 0, air.DazzleCdM2))
            : margin;
    }

    /// <summary>The oldest reading into the computer: the first starts the track, from the eye's guess at the target's
    /// course and speed.</summary>
    void PopReading(ref Fc fc, int f, in ShipState target, long now, Vec3d ownP, Vec3d ownV)
    {
        var r = pending[f * PendingCap + fc.PendHead];
        var computer = fc.Fit.Computer;
        if (!fc.Tracking)
        {
            fc.Tracker = FcTracker.Start(computer, r, Guess(computer, fc, target.Position, target.Forward * target.Speed), FcTracker.GuessSigma(computer, target.Speed));
            (fc.Tracking, fc.TrackMs, fc.OpenMs) = (true, now, now + (long)(settings.GunneryOpenS * 1000));
            fc.JudgeMs = now + (long)(computer.RejudgeS * 1000);
            fc.PlotMs = now;
            Plot(ref fc, f, ref r, r.Z);   // its first cut lies on the line
        }
        else
        {
            fc.Tracker.At(computer, r.Ms, now, ownP, ownV, out var p, out _);
            Plot(ref fc, f, ref r, p);
            if (Plausible(ref fc, r, p))
                fc.Tracker.Fix(computer, r);   // the guns follow the computer, the spots' correction on it (BL1z)
            fc.PlotMs = now;
        }
        fc.PendHead = (fc.PendHead + 1) % PendingCap;
        fc.PendCount--;
    }

    /// <summary>A reading's cut on the plot, and what the computer takes from it (docs/battle/balance.md BL1ab): the plot
    /// marked each instrument's cuts with its own symbol, so the plotter saw how each lay off the others
    /// (fire-control-research.md §3.5, the 1918 handbook) and drew his line through the middle of them all, not toward
    /// whichever had cut last. Each instrument's offset from the computer's line (p, where it has her at the reading's
    /// time) is the running mean of its last <see cref="SimSettings.PlotCuts"/> cuts; the reading goes in less its offset
    /// from the instruments' mean, each weighed by its spread.</summary>
    void Plot(ref Fc fc, int f, ref FcReading r, Vec3d p)
    {
        if (r.SensorSet != fc.SensorSet || r.Sensor >= fc.SensorCount)
            return;
        var set = sensors.AsSpan(f * MaxSensors, fc.SensorCount);
        ref var s = ref set[r.Sensor];
        double a = 1.0 / Math.Min(++s.Cuts, settings.PlotCuts);
        s.OffRangeM += a * (r.RangeM - Earth.Distance(r.From, p) - s.OffRangeM);
        s.OffBearingDeg += a * (Math.IEEERemainder(r.BearingDeg - Earth.Bearing(r.From, p), 360) - s.OffBearingDeg);
        (s.WRange, s.WBearing) = (1 / Math.Max(r.SigmaRangeM * r.SigmaRangeM, 1e-6), 1 / Math.Max(r.SigmaBearingRad * r.SigmaBearingRad, 1e-12));
        double mr = 0, wr = 0, mb = 0, wb = 0;
        foreach (ref readonly var o in set)
            if (o.Cuts > 0)
                (mr, wr, mb, wb) = (mr + o.WRange * o.OffRangeM, wr + o.WRange, mb + o.WBearing * o.OffBearingDeg, wb + o.WBearing);
        double range = Math.Max(r.RangeM - s.OffRangeM + mr / wr, 100), bearing = r.BearingDeg - s.OffBearingDeg + mb / wb;
        (r.Z, r.R11, r.R12, r.R22) = Track.Reading(r.From, range, bearing, r.SigmaRangeM, r.SigmaBearingRad);
        (r.RangeM, r.BearingDeg) = (range, Mod(bearing));
    }

    /// <summary>Whether a reading is one she could have made (the short-range bug hunt, 2026-10-18: "Sir, the target
    /// teleported 200 m!"): one more than 3 sigma off the computer's own prediction for its time (the prediction's doubt
    /// and the reading's together) is set aside, unless the one before was too: two in a row, and she really did move, or
    /// the old picture was wrong.</summary>
    bool Plausible(ref Fc fc, in FcReading r, Vec3d p)
    {
        double predicted = Earth.Distance(r.From, p);
        double sigma = Math.Sqrt(Math.Pow(fc.Tracker.PositionSigma(Earth.Forward(p, Earth.Bearing(r.From, p))), 2) + r.SigmaRangeM * r.SigmaRangeM);
        bool odd = Math.Abs(r.RangeM - predicted) > settings.ReadingGateSigmas * sigma;
        if (odd && !fc.SetAside)
        {
            fc.SetAside = true;
            return false;
        }
        fc.SetAside = false;
        return true;
    }

    /// <summary>The eye's guess at a target's velocity: its speed and course, each off by the battery's misjudgement of her
    /// (drawn once a target) in the computer's priors.</summary>
    Vec3d Guess(in ComputerRow c, in Fc fc, Vec3d position, Vec3d velocity)
    {
        double speed = velocity.Length, course = speed > 1e-6 ? Earth.Heading(position, velocity) : 0, prior = settings.GunneryErrors.Prior;
        double ds = fc.GuessSpeedZ * c.PriorKn * Handling.MsPerKnot * prior, dc = fc.GuessCourseZ * c.PriorDeg * prior;
        return Earth.Forward(position, course + dc) * Math.Max(speed + ds, 0);
    }

    /// <summary>Where a battery's guns are laid (the short-range bug hunt, 2026-10-18; fire-control.md F7's point blank
    /// taken in): the line is the layers' own, laid on the target as they see her now, with the computer's lead over the
    /// time of flight on it (how its solution has her move meanwhile: its motion across is what it gives the sights); the range is the
    /// computer's, which the spots correct. Point blank, where the danger space of her side covers twice the error of her
    /// present range by sight (the battery's first instrument, or the layer's eye, with the computer's residual ballistic
    /// bias: what spotting would take off), the range is that present range,
    /// laid on the middle of her side, and there's no ranging (fire-control-research/emergency-eyeball.md §6: "a hit is
    /// essentially guaranteed if the line is right"). A battery that sees nothing lays where its computer says.</summary>
    Vec3d Aim(ref Fc fc, int f, BallisticTable table, Vec3d muzzle, long now, Vec3d ownP, Vec3d ownV, double headingDeg, in ShipState target, in GunneryCrew crew)
    {
        var c = Predict(ref fc, table, muzzle, now, ownP, ownV, headingDeg);
        fc.PointBlank = false;
        if (!fc.Sees || double.IsNaN(fc.ElevationDeg))
            return c;
        // the lead is the way the solution has her move in the time of flight, not her velocity now times it: a turn
        // filter's arc (a straight lead threw away ½·v·ω·T², 70 m at 14 km on a 1°/s circle), a clock's rates
        fc.Tracker.At(fc.Fit.Computer, now, now, ownP, ownV, out var here, out _);
        here = Ordered(fc, here, ownP, headingDeg);
        var lead = (target.Position + (c - here)).Normalized() * Earth.Radius;
        double range = Earth.Distance(ownP, c), present = Earth.Distance(ownP, lead);
        double h0 = muzzle.Length - Earth.Radius;
        if (PointBlankAt(fc, f, table, h0, present, target, crew, out double danger, out double sigma, out double bias) is not { } pb)
            return c;
        if (pb && !fc.WasPointBlank && now - fc.PbLeftMs > 60_000)
        {
            // into the band: the sight set to her present range as judged then, nothing walked onto it yet (back within a
            // minute of leaving it, the layers keep the setting they had)
            var rz = stream.Derive((ulong)f).Derive((ulong)fc.Generation).Derive(0x5B0Au).Derive((ulong)now);
            (fc.SightZ, fc.PbCorrR) = (rz.NextGaussian(), 0);
        }
        if (!pb && fc.WasPointBlank)
            fc.PbLeftMs = now;
        fc.WasPointBlank = pb;
        if (pb)
            (fc.PointBlank, range) = (true, Math.Max(present * (1 + fc.PbCorrR) + 0.5 * danger + bias + sigma * fc.SightZ, 50));
        var p = ownP;
        var fw = Earth.Forward(p, Earth.Bearing(ownP, lead));
        Earth.Advance(ref p, ref fw, range);
        return p;
    }

    /// <summary>Whether laying at a target's present range by sight is point blank (<see cref="Aim"/>): her side's danger space
    /// at that range covers twice the error of it (the sight's, and the computer's residual ballistic bias: the propellant,
    /// the air, the gun's wear; small at short range, a share of the range), or the layers read their own falls finely
    /// enough to walk them onto her (their spotting error from their guns' height under a quarter of it: the user,
    /// 2026-10-18); in at the test, out only with a margin past it (no flicking at its edge). Null where the guns can't
    /// reach her.</summary>
    bool? PointBlankAt(in Fc fc, int f, BallisticTable table, double h0, double present, in ShipState target, in GunneryCrew crew,
        out double danger, out double sigma, out double bias)
    {
        (danger, sigma, bias) = (0, 0, 0);
        // her danger space at her present range (not the computer's: a clock run past her closest approach has her short)
        double e = table.ElevationFor(present, h0, 1, fc.ElevationDeg);
        if (double.IsNaN(e))
            return null;
        var tc = target.Handling.Class;
        double side = Math.Max(tc.Hull.DeckTop(0) - tc.Vertical.Waterline, 1);
        double fall = table.Fall(e, h0).FallDeg;
        danger = side / Math.Tan(double.DegreesToRadians(Math.Max(fall, 0.05)));
        (sigma, bias) = PresentRangeError(fc, f, crew, present);
        double resid = fc.BallR * present;
        double own = settings.SpotOpticK * settings.GunneryCombat * present * present / Math.Max(h0, 1);
        double margin = fc.WasPointBlank ? 0.75 : 1;
        return danger * margin >= 2 * Math.Sqrt(sigma * sigma + bias * bias + resid * resid) || own <= 0.25 * danger / margin;
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
    Vec3d Predict(ref Fc fc, BallisticTable table, Vec3d muzzle, long now, Vec3d ownPosition, Vec3d ownVelocity, double headingDeg)
    {
        double h0 = muzzle.Length - Earth.Radius;
        var computer = fc.Fit.Computer;
        fc.Tracker.At(computer, now + (long)(fc.TofS * 1000), now, ownPosition, ownVelocity, out var p, out _);
        p = Ordered(fc, p, ownPosition, headingDeg);
        for (int i = 0, n = fc.TofS > 0 ? 2 : 3; i < n; i++)
        {
            double e = table.ElevationFor(Earth.Distance(muzzle, p), h0, 1, fc.ElevationDeg);
            if (double.IsNaN(e))
                break;
            fc.ElevationDeg = e;
            fc.TofS = table.Fall(e, h0).Tof;
            fc.Tracker.At(computer, now + (long)(fc.TofS * 1000), now, ownPosition, ownVelocity, out p, out _);
            p = Ordered(fc, p, ownPosition, headingDeg);
        }
        if (!computer.RangeClock)
        {
            // no clock to run it on (FL4): the range passed at the last plot stays on the sights, the trainers on her bearing
            if (fc.HeldMs != fc.PlotMs)
                (fc.HeldRangeM, fc.HeldMs) = (Earth.Distance(ownPosition, p), fc.PlotMs);
            var q = ownPosition;
            var fw = Earth.Forward(q, Earth.Bearing(ownPosition, p));
            Earth.Advance(ref q, ref fw, fc.HeldRangeM);
            p = q;
        }
        return p;
    }

    /// <summary>Own ship as a machine fed her motion reckons her (docs/battle/fc-ladder-check.md FL1), carried on to now: her
    /// place dead reckoned from where it had her, on her course (the gyro's repeat motor drives the table through gearing:
    /// it has her course as she turns, unless a man follows the gyro's pointer by hand, the Type 92) at the speed it has,
    /// each brought toward hers by a first-order lag (her speed a Forbes log read and set by hand, a pitometer's own
    /// response; the ladder's own rung scales them). With no lag it has her exactly. The gyrocompass's own error after a
    /// manoeuvre is left out: it turns her course and every bearing together, so only its rate reaches the solution, some
    /// 15 m at 15 km at most (research/simulation/ship-motion/own-motion-inputs.md §3).</summary>
    void Reckon(ref Fc fc, in ShipState ship, long now)
    {
        var c = fc.Fit.Computer;
        double ek = settings.GunneryErrors.Own, tc = ek * c.OwnCourseLagS, ts = ek * c.OwnSpeedLagS, dt = (now - fc.OwnHatMs) / 1000.0;
        fc.OwnHatMs = now;
        if (!c.HelmFree || tc <= 0 && ts <= 0)
        {
            (fc.OwnHatP, fc.OwnHatV, fc.OwnHatHeadingDeg, fc.OwnHatSpeed) = (ship.Position, ship.Forward * ship.Speed, ship.HeadingDeg, ship.Speed);
            return;
        }
        if (dt <= 0)
            return;
        double heading = tc > 0 ? Mod(fc.OwnHatHeadingDeg + Wrap(ship.HeadingDeg - fc.OwnHatHeadingDeg) * (1 - Math.Exp(-dt / tc))) : ship.HeadingDeg;
        double speed = ts > 0 ? fc.OwnHatSpeed + (ship.Speed - fc.OwnHatSpeed) * (1 - Math.Exp(-dt / ts)) : ship.Speed;
        // on along the course it has her on over the second (the mean of the one it had and has now), at the mean speed
        var p = fc.OwnHatP;
        var fw = Earth.Forward(p, fc.OwnHatHeadingDeg + 0.5 * Wrap(heading - fc.OwnHatHeadingDeg));
        Earth.Advance(ref p, ref fw, 0.5 * (fc.OwnHatSpeed + speed) * dt);
        (fc.OwnHatP, fc.OwnHatHeadingDeg, fc.OwnHatSpeed) = (p, heading, speed);
        fc.OwnHatV = Earth.Forward(p, heading) * speed;
    }

    /// <summary>A stable vertical's own tilt carried on to now (FL7): drifting one way (drawn as each turn begins) while her
    /// turn's acceleration has its erection cut out, erected again after. None on a mount without one: a director lays to
    /// the horizon.</summary>
    void Vertical(ref Fc fc, int f, in ShipState ship, long now)
    {
        double dt = (now - fc.SvMs) / 1000.0;
        fc.SvMs = now;
        if (!fc.Fit.Mount.StableVertical || dt <= 0)
            return;
        bool cut = Math.Abs(ship.Speed * ship.YawRate) > settings.StableVerticalCutMs2;
        if (cut && !fc.SvCut)
        {
            var r = stream.Derive((ulong)f).Derive((ulong)fc.Generation).Derive(0x5F1u).Derive((ulong)now);
            double a = 2 * Math.PI * r.NextDouble();
            (fc.SvDriftE, fc.SvDriftN) = (Math.Cos(a), Math.Sin(a));
        }
        fc.SvCut = cut;
        if (cut)
        {
            double d = settings.StableVerticalDriftArcminMin / 60 * Math.PI / 10_800 * dt;   // arc-minutes a minute, rad a second
            (fc.SvTiltE, fc.SvTiltN) = (fc.SvTiltE + d * fc.SvDriftE, fc.SvTiltN + d * fc.SvDriftN);
        }
        else
        {
            double k = Math.Exp(-dt / settings.StableVerticalErectS);
            (fc.SvTiltE, fc.SvTiltN) = (fc.SvTiltE * k, fc.SvTiltN * k);
        }
    }

    /// <summary>A point by own ship (an instrument's) where a machine fed her motion reckons it: as far off her reference
    /// point as it truly is (FL1).</summary>
    static Vec3d Reckoned(in Fc fc, in ShipState ship, Vec3d at) => (fc.OwnHatP + (at - ship.Position)).Normalized() * Earth.Radius;

    /// <summary>A machine's point (where it has the target) as its gun orders put it from own ship: its range, and its
    /// bearing off her head by the course it has, from where it reckons her, laid off from where she is (FL1); a machine not
    /// fed her motion works from her as she is.</summary>
    static Vec3d Ordered(in Fc fc, Vec3d p, Vec3d ownP, double headingDeg)
    {
        if (!fc.Fit.Computer.HelmFree)
            return p;
        double range = Earth.Distance(fc.OwnHatP, p), bearing = Earth.Bearing(fc.OwnHatP, p) - fc.OwnHatHeadingDeg + headingDeg;
        var q = ownP;
        var fw = Earth.Forward(q, bearing);
        Earth.Advance(ref q, ref fw, range);
        return q;
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
        if (slot < 0 || tslot < 0)
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
            bool bears = ms.Rounds > 0 && !store.Combat.Illuminating(slot, mounts[j]) && cond.Lays(mounts[j], fc.Local) && fc.OrderElevationDeg <= mr.MaxElevationDeg && CombatStore.InArcs(cls, mr, fc.OrderBearingDeg) &&
                         !GunWet(fc.Shooter, slot, ship, cls, cond, mounts[j], now);
            (bearingGuns, bearingMounts) = bears ? (bearingGuns + cond.Guns(mounts[j]), bearingMounts + 1) : (bearingGuns, bearingMounts);
            firing[j] = bears && ms.LoadedMs <= now &&
                        Math.Abs(Wrap(ms.TrainTo - fc.OrderBearingDeg)) <= tol &&
                        Math.Abs(CombatStore.BearingAt(ms, mr, now) - ms.TrainTo) <= tol && Math.Abs(CombatStore.ElevationAt(ms, mr, now) - ms.ElevTo) <= tol;
            // each mount fires what its guns hold, or the first load of the magazine order it has rounds of
            fireLoad[j] = firing[j] ? store.Combat.FiresNext(slot, mounts[j]) : -1;
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
        var crew = CrewOf(ship);
        int tableIndex = fc.Table;
        var table = shells.Ballistics[tableIndex];
        var frame = ship.Frame;
        var velocity = ship.Forward * ship.Speed;
        var muzzle0 = Muzzle(cls, frame, mounts[0]);
        double h0 = muzzle0.Length - Earth.Radius;
        var p = Aim(ref fc, f, table, muzzle0, now, ship.Position, velocity, ship.HeadingDeg, store[tslot], crew);
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

        // the range correction: ranging, from the spots' bracket (a ladder's salvo stepped about it); firing for effect, the
        // walk's, as the spots left it; then the salvo's common errors, each where it arises (fire_control_ref.py engage(),
        // "fire")
        if (phase != FirePhase.Effect)
            fc.CorrR = fc.Bracket.Aim(now, fc.DriftMs);
        // a ladder's rungs at the ranging step, the computer's doubt (balance.md BL1t: never wider than the doctrine's own
        // step; a ladder of 366 m about a solution good to 100 m threw two salvos of three a ship's danger space off); and
        // once its falls have closed the bracket, inside it at a quarter of its width, so each group halves it as every
        // navy's bracket did (the RN's 400-200-100, the USN's "reverse and halve"; at the floor, the rocking ladder's
        // ±100 yd): at a fixed step the next group fell where the last had and never closed in
        double rung = Math.Min(dis.LadderStepM, fc.Bracket.Step);
        if (fc.Bracket.Closed)
            rung = Math.Min(rung, 0.25 * (fc.Bracket.HiAt(now, fc.DriftMs) - fc.Bracket.LoAt(now, fc.DriftMs)));
        double ladder = phase == FirePhase.Ranging && dis.LadderStepM > 0 && !pointBlank ? (fc.Rung - 0.5 * (dis.InFlight - 1)) * rung : 0;
        var r = stream.Derive((ulong)f).Derive((ulong)fc.Generation).Derive(0x5A1u).Derive((ulong)fc.Salvos);
        // the layer's stress (BL1d): his station's duress, the director's (its first instrument's) or the guns' own
        int layerAt = mount.DirectorFiring && fc.SensorCount > 0 && sensors[f * MaxSensors].Eye >= 0 ? cls.Eyes[sensors[f * MaxSensors].Eye].Component
            : cls.Mounts[mounts[0]].Component;
        double stress = Steadiness.Stress(cls, cond, layerAt, now, settings);
        // the sights shaking at speed (BL1h): the director's where it stands, else the guns' own (a turret or a deck mount)
        double shake = mount.DirectorFiring && fc.SensorCount > 0 ? Vibration(cls, ship, sensors[f * MaxSensors].Support, sensors[f * MaxSensors].X)
            : Vibration(cls, ship, InstrumentSupport.Turret, cls.Mounts[mounts[0]].X);
        // and the air on his sight (BL1i): a grey shape at the edge of what he can make out
        var errs = settings.GunneryErrors;
        // a director's layer by what the director sees; each gun's own layer by what he sees from his sight (ES6: the 1912
        // trial's turret layers in Orion's smoke, where Thunderer's director aloft saw her), the salvo's guns' as one spread
        double haze = SightModel.HazeFactor(fc.Margin, settings.AimHazePower, settings);
        if (!mount.DirectorFiring)
        {
            var lms = layerMargin.AsSpan(f * MaxLayers, MaxLayers);
            double sum = 0;
            int laid = 0;
            for (int j = 0; j < mounts.Length && j < MaxLayers; j++)
                if (firing[j] && !double.IsNaN(lms[j]))
                    (sum, laid) = (sum + Math.Pow(SightModel.HazeFactor(lms[j], settings.AimHazePower, settings), 2), laid + 1);
            if (laid > 0)
                haze = Math.Sqrt(sum / laid);
        }
        double layRad = errs.Laying * mount.LayMil * FireControl.Mil * crew.Lay * combat * stress * shake * haze;
        double eEl = mount.DirectorFiring ? r.NextGaussian() * layRad * perRad : 0, eTr = mount.DirectorFiring ? r.NextGaussian() * layRad * range : 0;
        // own ship's turns cost a machine fed her motion what its own course and speed inputs lag (the computer's own ship,
        // Reckon: docs/battle/fc-ladder-check.md FL1), no draw here
        // no salvo-to-salvo draw of its own (balance.md BL1ac): the reference's salvo_pct lumped what the chain now draws
        // in place, the computer's wander (its track, the plot), the director's laying (above), the stable element's and
        // the pointers' lag at the roll (below), the salvo's mean muzzle velocity and each gun's calibration (its shells'
        // own draws, the measured pattern's)
        // the day's ballistics the machine hasn't (ES3): the wind it doesn't know along the shells' path, its air's
        // density and its guns' muzzle velocity as off as it has them, at this flight's own sensitivities
        var flight = BallisticResidual.Of(table, e0, h0);
        var wx = world.Weather.At(ship.Position, world.Time);
        var wind10 = Earth.Forward(p, wx.WindFromDeg + 180) * wx.WindMs;
        var (br, bl) = BallisticResidual.Fall(computer, flight, wind10, fc.WindZ, fc.DensityZ, fc.MvZ, u, w, errs.Wind, errs.Bias);
        // and this salvo's charges' own departure, common to its guns (ES7): the floor no spot takes out
        br += errs.Charges * settings.SalvoMvPct * flight.PerMvPct * r.NextGaussian();
        (fc.BallR, fc.BallD) = BallisticResidual.Doubt(computer, flight, wx.WindMs, range);
        double dr = (pointBlank ? 0 : fc.CorrR + ladder) + eEl + br;
        double dl = fc.CorrD * range + eTr + bl;
        if (mount.StableVertical && (fc.SvTiltE != 0 || fc.SvTiltN != 0))
        {
            // the vertical's own tilt (FL7): along the line of fire it lays the guns' elevation off, across it cants their
            // trunnions; the own rung's, as her motion's
            var tilt = Earth.East(ship.Position) * fc.SvTiltE + Earth.North(ship.Position) * fc.SvTiltN;
            dr += errs.Own * Vec3d.Dot(tilt, u) * perRad;
            dl += errs.Own * Vec3d.Dot(tilt, w) * Math.Tan(double.DegreesToRadians(e0)) * range;
        }
        var (sr, sd) = FireControl.Dispersion(table, row.PatternPct, range + dr, e0, h0, settings, flight.PerMvPct, perRad);
        if (!mount.DirectorFiring)
            (sr, sd) = (Math.Sqrt(sr * sr + Math.Pow(layRad * perRad, 2)), Math.Sqrt(sd * sd + Math.Pow(layRad * range, 2)));
        if (fc.Roll.AmplitudeRad > 0 || fc.Pitch.AmplitudeRad > 0 || cond.Attitude.HeelDeg != 0 || ship.YawRate != 0)
        {
            // the roll (BL1b; research §4.1, §4.3): the layer's timing lag at her roll rate as his sight comes on (the gear's lag
            // following it, a stable vertical's contacts), into elevation as she rolls across the line of fire and into line by
            // the trunnions' tilt toward the bow and stern, which a list tilts too; his sights correct a share of the tilt. A
            // director's layer's slip is the salvo's, each gun's layer's his own (dispersion); a list is every gun's.
            var rr = stream.Derive((ulong)f).Derive((ulong)fc.Generation).Derive(0x5A2u).Derive((ulong)fc.Salvos);
            double rel = double.DegreesToRadians(fc.OrderBearingDeg), tanE = Math.Tan(double.DegreesToRadians(e0)), keep = 1 - mount.CrossLevel;
            double tau = errs.Laying * stress * (fc.OnRoll ? mount.RollTauS * crew.RollTiming : mount.StableVertical ? mount.RollTauS : settings.ContinuousAimTauS);
            double omega = fc.Roll.RateAtLevel(ref rr), slip = tau * omega * rr.NextGaussian();
            // and her pitch (BL1h): the same lag at its rate, into elevation toward the bow and stern, cross-level abeam
            double pslip = tau * fc.Pitch.RateAtLevel(ref rr) * rr.NextGaussian();
            // and her yaw under the helm (BL1h; research §4.5): the trainer's lag at her rate of turn, into line (the sea's
            // own yaw, a degree or two, left out)
            double yslip = tau * ship.YawRate * rr.NextGaussian();
            // fired anywhere in the roll (following it), the roll's own tilt at that instant; on the roll, at its level
            double at = fc.OnRoll ? 0 : errs.Laying * fc.Roll.AmplitudeRad * Math.Sin(2 * Math.PI * rr.NextDouble());
            double heel = double.DegreesToRadians(cond.Attitude.HeelDeg) * Math.Cos(rel);
            if (mount.DirectorFiring)
            {
                dr += (slip * Math.Sin(rel) + pslip * Math.Cos(rel)) * perRad;
                dl += keep * Math.Atan(Math.Sin(heel + (at + slip) * Math.Cos(rel) - pslip * Math.Sin(rel)) * tanE) * range + yslip * range;
            }
            else
            {
                double each = tau * fc.Roll.RateSigmaRadS, peach = tau * fc.Pitch.RateSigmaRadS;
                double el = Math.Sqrt(Math.Pow(each * Math.Sin(rel), 2) + Math.Pow(peach * Math.Cos(rel), 2));
                double xl = keep * Math.Sqrt(Math.Pow(each * Math.Cos(rel), 2) + Math.Pow(peach * Math.Sin(rel), 2)) * tanE;
                (sr, sd) = (Math.Sqrt(sr * sr + Math.Pow(el * perRad, 2)), Math.Sqrt(sd * sd + Math.Pow(xl * range, 2) + Math.Pow(tau * ship.YawRate * range, 2)));
                dl += keep * Math.Atan(Math.Sin(heel + at * Math.Cos(rel)) * tanE) * range;
            }
        }
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
        // the spotter's eye through the same air (BL1i): over or short of a splash low in haze
        double spot = settings.SpotOpticK * combat * range * range / Math.Max(fc.SpotHeightM, 1) * SightModel.HazeFactor(fc.Margin, settings.SpotHazePower, settings);
        sv = new Salvo
        {
            Alive = true, Generation = sv.Generation + 1, Fc = f, FcGeneration = fc.Generation, Target = fc.Target, CorrR = fc.CorrR + ladder, CorrD = fc.CorrD,
            PatternM = 4 * sr, PatternAcrossM = 4 * sd, RangeM = range, SpotSigmaM = spot, Common = errs.Spotting * 0.5 * spot * r.NextGaussian(), U = u, W = w, MinX = double.MaxValue,
            MaxX = double.MinValue, TargetLengthM = store[tslot].Handling.Class.Silhouette.LengthM, FiredMs = now,
            AimErrM = Vec3d.Dot(p - truth, u), AimErrAcrossM = Vec3d.Dot(p - truth, w), Phase = phase, LadderM = ladder,
            PointBlank = pointBlank, TrueRangeM = Earth.Distance(ship.Position, tgt.Position), PbCorrR = fc.PbCorrR,
            CalibreMm = row.CalibreMm, Shooter = fc.Shooter, FirstDownMs = -1, YawDegS = (float)double.RadiansToDegrees(ship.YawRate),
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
                var point = centre + u * (errs.Dispersion * sr * r.NextGaussian()) + w * (errs.Dispersion * sd * r.NextGaussian());
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
            {
                shells.Fired(world, fc.Shooter, ship.ClassIndex, mounts[j], salvo, away, muzzle, first, firstEl);
                world.Smoke.Gun(world, slot, cls, mounts[j], away, muzzle, first, firstEl);   // its smoke (docs/battle/smoke.md Z4)
            }
            // the reload stretched by the gun crew at work, the hoists by the handling men (docs/battle/crew.md N2), both
            // by hand with its power off
            var mc = store.Combat.Condition(slot);
            double hand = mc.ByHand(mounts[j], now) ? settings.HandReload : 1;
            var flash = world.Damage.FlashOf(ship.Side, mc);
            store.Combat.Fired(slot, cls, mounts[j], fireLoad[j], barrels, now, hand * Math.Max(1, crew.Load * flash.LoadShare) / mc.MountCrew[mounts[j]],
                hand * flash.HoistShare / DesignerPlaceholders.HoistShare / mc.MountSupply[mounts[j]], flash.StackShare);
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
        fc.SpoiledMs = -1;   // a salvo held for her turn waits afresh
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

    /// <summary>Room for the splash rings of this many ship slots (BL1c): grown when a battery engages, so a fall never
    /// allocates.</summary>
    void SplashRoom(int slots)
    {
        if (splashHead.Length >= slots)
            return;
        int n = Math.Max(slots, splashHead.Length * 2);
        Array.Resize(ref splashHead, n);
        Array.Resize(ref splashMs, n * SplashRing);
        Array.Resize(ref splashShooter, n * SplashRing);
        Array.Resize(ref splashCal, n * SplashRing);
        Array.Resize(ref splashAt, n * SplashRing);
        Array.Resize(ref splashPeak, n * SplashRing);
    }

    /// <summary>Whether a spotter tells a salvo's fall from the others about its target (BL1c): a salvo of
    /// <see cref="SimSettings.SalvoIdentifiedShells"/> or more always, one of fewer shells with the chance
    /// 1/(1 + k·N), N the other ships' falls of its calibre band near her within the window of its own (his own ship's
    /// guns firing independently he reads as one group, from the balance of their overs and shorts: research §5).</summary>
    bool Told(in Salvo sv, int tslot, ref RandomStream r)
    {
        if (sv.Landed >= settings.SalvoIdentifiedShells || tslot < 0 || tslot >= splashHead.Length)
            return true;
        int n = 0, at = tslot * SplashRing;
        long lo = sv.FirstDownMs - settings.SplashWindowMs, hi = sv.LastDownMs + settings.SplashWindowMs;
        for (int k = 0; k < SplashRing; k++)
            if (splashShooter[at + k] != sv.Shooter && splashMs[at + k] >= lo && splashMs[at + k] <= hi && splashMs[at + k] > 0 &&
                Math.Abs(splashCal[at + k] / sv.CalibreMm - 1) <= settings.SplashCalibreBand)
                n++;
        return n == 0 || r.NextDouble() < 1 / (1 + settings.SplashConfusion * n);
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

    /// <summary>A shell of a salvo down (from the shell system, at its end, with the angle it fell at): where it fell
    /// against the target, as it is and as the spotter will judge it; the last one schedules the spot.</summary>
    internal void Landed(World world, int si, Vec3d end, int hit, double fallDeg)
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
            double seen = hit == sv.Target ? 0 : x + sv.Common + settings.GunneryErrors.Spotting * sv.SpotSigmaM * r.NextGaussian();
            // line first: a splash is judged over or short only in line with her (against her hull, hidden behind it or
            // in front of it), within her width across the line of sight and a margin; one off for line could be either
            var tgt = store[tslot];
            var tm = tgt.Handling.Class.Motion;
            double ca = Math.Abs(Vec3d.Dot(tgt.Forward, sv.U)), half = 0.5 * (tm.LengthM * Math.Sqrt(Math.Max(1 - ca * ca, 0)) + tm.BeamM * ca);
            if (Math.Abs(y) <= half + settings.SpotLineMarginM)
            {
                if (seen > 0)
                    sv.Over++;
                else if (seen < 0)
                    sv.Short++;
            }
            if (hit == sv.Target)
                sv.Hits++;
            // whose splash (BL1c): a fall near her goes into her ring, for every spotter watching her
            long down = world.Time.Ms;
            (sv.FirstDownMs, sv.LastDownMs) = (sv.FirstDownMs < 0 ? down : sv.FirstDownMs, down);
            if (tslot < splashHead.Length && x * x + y * y <= settings.SplashNearM * settings.SplashNearM)
            {
                int k = tslot * SplashRing + splashHead[tslot];
                (splashMs[k], splashShooter[k], splashCal[k]) = (down, sv.Shooter, (float)sv.CalibreMm);
                (splashAt[k], splashPeak[k]) = (end, hit >= 0 ? 0 : (float)SplashColumn.PeakM(sv.CalibreMm, fallDeg));
                splashHead[tslot] = (splashHead[tslot] + 1) % SplashRing;
            }
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
                sv.TrueRangeM, sv.PointBlank, sv.AimErrM + (sv.PointBlank ? 0 : sv.CorrR), sv.Target, sv.YawDegS));
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
        if (world.Ships.Store.SlotOf(sv.Target) is int ts and >= 0)
            world.Damage.Salvo(world, ts, straddle);   // a green crew straddled, or fired on lit (docs/battle/night.md Q8)
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
                Span<double> smoke = stackalloc double[SmokeSystem.PairTaus];
                var air = AirAt(world, ship.Position, slot, tslot, smoke);
                for (int k = 0; k < fc.SensorCount && !sees; k++)
                {
                    ref readonly var s = ref sensors[sv.Fc * MaxSensors + k];
                    sees = MarginOf(ship.Handling.Class, s, fc.Fit.Optics, frame.ToWorld(s.X, s.Y, 0), ship.HeadingDeg, target, air, smoke, slot, frame, world.Time.Ms) >= 1;
                }
            }
            var sign = sv.Hits > 0 || sv.Over > 0 && sv.Short > 0 ? FireControl.Sign.Straddle : sv.Over > 0 ? FireControl.Sign.Over :
                sv.Short > 0 ? FireControl.Sign.Short : FireControl.Sign.Doubtful;
            long now = world.Time.Ms;
            // whose splash (BL1c): a fall he can't tell from the others about her is doubtful, or read from the wrong one; a
            // hit he sees for his own, and point blank his layers watch their own guns' fall
            bool told = true;
            if (sees && !sv.PointBlank && sv.Hits == 0)
            {
                var rw = stream.Derive(0x5B0Cu).Derive((ulong)si).Derive((ulong)sv.Generation);
                told = Told(sv, tslot, ref rw);
                if (!told)
                    sign = rw.NextDouble() < settings.SplashMisreadShare ? (rw.NextDouble() < 0.5 ? FireControl.Sign.Over : FireControl.Sign.Short) : FireControl.Sign.Doubtful;
            }
            if (sees && !sv.PointBlank)
            {
                // each spot against the correction its salvo was fired with; a crew may pyramid, putting it on the one in
                // force (F6: the green most often)
                var rp = stream.Derive(0x5B09u).Derive((ulong)si).Derive((ulong)sv.Generation);
                bool pyramid = rp.NextDouble() < CrewOf(ship).Pyramid;
                if (sign != FireControl.Sign.Doubtful)
                    SpotRange(ref fc, sv, sign, pyramid, now);
                // the line, spotted whether the range could be or not (off for line, it's corrected first): an angle, so
                // it holds as the range changes
                var r = stream.Derive(0x5B08u).Derive((ulong)si).Derive((ulong)sv.Generation);
                double defl = sv.SumY / Math.Max(sv.Landed, 1) + settings.GunneryErrors.Spotting * settings.SpotDeflectionMil * FireControl.Mil * sv.RangeM * r.NextGaussian();
                if (told && Math.Abs(defl) > 0.25 * sv.PatternAcrossM)
                    fc.CorrD = (pyramid ? fc.CorrD : sv.CorrD) - settings.SpotDeflectionGain * defl / sv.RangeM;
            }
            else if (sees && sv.Hits == 0)
            {
                // point blank: the layers walk their own fall onto her (the user, 2026-10-18), reading how far over or short
                // and off the line it fell (k·R²/h from their height: a few metres close in); a hit, and they hold the lay
                // (this branch isn't taken); a miss, they take half of it off, as magnitude spots go (half gain, a dead band
                // of half a pattern, so the gun's own scatter isn't chased: fire-control-research.md §9)
                var r = stream.Derive(0x5B0Bu).Derive((ulong)si).Derive((ulong)sv.Generation);
                double x = sv.SumX / Math.Max(sv.Landed, 1) + settings.GunneryErrors.Spotting * sv.SpotSigmaM * r.NextGaussian();
                if (Math.Abs(x) > 0.5 * sv.PatternM)
                    fc.PbCorrR = sv.PbCorrR - 0.5 * x / Math.Max(sv.TrueRangeM, 1);
                double defl = sv.SumY / Math.Max(sv.Landed, 1) + settings.GunneryErrors.Spotting * settings.SpotDeflectionMil * FireControl.Mil * sv.RangeM * r.NextGaussian();
                if (Math.Abs(defl) > 0.25 * sv.PatternAcrossM)
                    fc.CorrD = sv.CorrD - settings.SpotDeflectionGain * defl / sv.RangeM;
            }
            bool walking = fc.Phase == FirePhase.Effect;
            Spotted?.Invoke(new SpotReport(sv.FiredMs, fc.Shooter, fc.Battery, sees, sign,
                walking ? fc.CorrR - SpotStep(fc) : fc.Bracket.LoAt(now, fc.DriftMs), walking ? fc.CorrR + SpotStep(fc) : fc.Bracket.HiAt(now, fc.DriftMs),
                fc.CorrR, fc.Phase, told, fc.DriftMs, fc.PosSigmaM, (int)fc.Fit.Computer.Tracker, sv.SpotSigmaM, fc.SpotHeightM));
        }
        FreeSalvo(si);
    }

    /// <summary>A salvo's range spot (over, short or straddling): ranging, into the bracket, a straddle opening fire for
    /// effect; firing for effect, the walk.</summary>
    void SpotRange(ref Fc fc, in Salvo sv, FireControl.Sign sign, bool pyramid, long now)
    {
        double before = fc.CorrR;
        fc.LastSign = (int)sign;
        // the correction its salvo was fired with, as it stands on today's solution; a crew that pyramids puts the spot on
        // the one in force (F6: the green most often)
        double at = pyramid ? fc.CorrR + sv.LadderM : sv.CorrR;
        if (fc.Phase == FirePhase.Effect)
            Walk(ref fc, sign, at, now);
        else
        {
            double pattern = Math.Max(settings.BracketFloorM, settings.BracketFloorPatterns * sv.PatternM);
            fc.Bracket.Spot(sign, at, now, fc.DriftMs, sv.PatternM, pattern);
            fc.CorrR = fc.Bracket.Aim(now, fc.DriftMs);
            if (sign == FireControl.Sign.Over)
                fc.OverAt = at;
            else if (sign == FireControl.Sign.Short)
                fc.ShortAt = at;
            // she's found by a straddle, or by the fall crossing her within a pattern: an over and a short fired that close
            // (the RN's "correct until the fall crosses the target", the USN's "never below the pattern"); the halving after
            // it is fire for effect's own (BL1y: the bracket's bounds, widened by the computer's doubt of the rate as they
            // aged, never closed with perfect information)
            bool crossed = fc.OverAt - fc.ShortAt is > 0 and var gap && gap <= pattern;
            if (sign == FireControl.Sign.Straddle || crossed)
            {
                if (fc.EffectMs < 0)
                {
                    fc.EffectMs = now;
                    // its record (BL1ae): how long its track took to settle, and how many of its cycles it ranged
                    if (ships.Store.SlotOf(fc.Shooter) is int rs and >= 0 && fc.OpenMs >= fc.StartMs)
                    {
                        var lo = ships.Store.Combat.Loadout(rs);
                        ships.Store.Combat.Ranged(rs, fc.Battery, (fc.OpenMs - fc.StartMs) / 1000.0,
                            (now - fc.OpenMs) / 1000.0 / RangingCycleS(lo, fc.Battery, fc.RangeM));
                    }
                }
                double width = sign == FireControl.Sign.Straddle ? fc.Bracket.HiAt(now, fc.DriftMs) - fc.Bracket.LoAt(now, fc.DriftMs) : fc.OverAt - fc.ShortAt;
                if (sign != FireControl.Sign.Straddle)
                    fc.CorrR = 0.5 * (fc.OverAt + fc.ShortAt);
                (fc.Phase, fc.Misses, fc.WalkStep, fc.WalkN, fc.WalkDir, fc.WalkRunM, fc.WalkRun) = (FirePhase.Effect, 0, 0.5 * width, 1, 0, 0, 0);
                (fc.OverAt, fc.ShortAt) = (double.NaN, double.NaN);
            }
        }
        // the layers lay on again after a correction; the range rate is the computer's, from its readings (the rate spot,
        // the RN's "down 200, rate down 100", put the spots' noise into the clock: with the walk it cost 20-60 m of the
        // fall's centre on every bench, 2026-11-02)
        if (Math.Abs(fc.CorrR - before) >= 1)
            fc.SettleMs = now + (long)(1000 * settings.LayerSettleS);
    }

    /// <summary>Fire for effect's spotting: the spots' running mean (the user, 2026-11-02: "walk basically arbitrary error
    /// into target and keep it there, eventually"; balance.md BL1w-x). Each spot is a noisy sign of where the falls' centre
    /// lies, the salvo's own error in it: an over steps the correction in force down, a short up, by a step that shrinks
    /// as the evidence comes in, half the bracket's width when fire for effect began over the spots' count (each time the
    /// fall crosses her, or a straddle says it's on her), never under <see cref="SimSettings.SpotStepFloorM"/>. Calls the
    /// same way don't shrink it: that's an offset to walk off, not noise; from the third in a row it doubles back toward
    /// its first (two alike are a coin's flip, three a target that has moved: a zigzag's leg). So the correction settles where an over and a
    /// short are as likely, the falls' mean on her, and stops chasing the salvos' own scatter. A spot of a salvo fired
    /// before the correction last moved, saying what that move already answered, changes nothing. When the calls the same
    /// way, <see cref="SimSettings.RangeLostCalls"/> of them in a row at least (fewer come of the salvos' own scatter: two
    /// alike are a coin's flip), have walked it a whole ranging step without the fall crossing, the range is clearly lost
    /// (the user: "dropping to ladder should only happen when one has clearly lost the range"): back to ranging, the
    /// bracket open beyond that salvo's correction.</summary>
    void Walk(ref Fc fc, FireControl.Sign sign, double at, long now)
    {
        if (sign == FireControl.Sign.Straddle)
        {
            (fc.WalkN, fc.Misses, fc.WalkRunM, fc.WalkRun) = (fc.WalkN + 1, 0, 0, 0);
            return;
        }
        int d = sign == FireControl.Sign.Over ? -1 : 1;
        if (d * (fc.CorrR - at) > 0.5)
            return;   // already moved past it that way
        fc.Misses++;
        if (fc.WalkDir == -d)
            (fc.WalkN, fc.WalkRunM, fc.WalkRun) = (fc.WalkN + 1, 0, 0);   // crossed her
        if (fc.WalkRun >= settings.RangeLostCalls - 1 && fc.WalkRunM >= fc.Fit.Computer.FirstStepM)
        {
            fc.Bracket = FireControl.Bracket.Beyond(sign, at, now, FirstStep(fc));
            (fc.Phase, fc.Misses, fc.WalkDir, fc.WalkRunM, fc.WalkRun, fc.Rung) = (FirePhase.Ranging, 0, 0, 0, 0, 0);
            (fc.OverAt, fc.ShortAt) = sign == FireControl.Sign.Over ? (at, double.NaN) : (double.NaN, at);
            fc.CorrR = fc.Bracket.Aim(now, fc.DriftMs);
            return;
        }
        if (fc.WalkRun >= 2)
            fc.WalkN = Math.Max(fc.WalkN / 2, 1);   // a third call the same way: she's moved, the step grows back
        double step = SpotStep(fc);
        (fc.WalkDir, fc.WalkRunM, fc.WalkRun) = (d, fc.WalkRunM + step, fc.WalkRun + 1);
        fc.CorrR += d * step;
    }

    /// <summary>Fire for effect's step now, m (<see cref="Walk"/>).</summary>
    double SpotStep(in Fc fc) => Math.Max(settings.SpotStepFloorM, fc.WalkStep / Math.Max(fc.WalkN, 1));

    /// <summary>The solution disturbed (the target seen to turn, a plain computer's own course gone stale): a battery firing
    /// for effect goes back to ranging, its bracket closed round the walk's correction at the first step; one ranging opens
    /// its bracket round its middle to the first step.</summary>
    void Disturbed(ref Fc fc, long now)
    {
        fc.Bracket.Step = FirstStep(fc);
        if (fc.Phase == FirePhase.Effect)
            (fc.Bracket, fc.Phase, fc.Misses, fc.WalkDir, fc.WalkRunM, fc.WalkRun) =
                (FireControl.Bracket.About(fc.CorrR, fc.Bracket.Step, now, fc.Bracket.Step), FirePhase.Ranging, 0, 0, 0, 0);
        else
            fc.Bracket.Widen(now, fc.DriftMs, fc.Bracket.Step);
        (fc.Rung, fc.OverAt, fc.ShortAt) = (0, double.NaN, double.NaN);
    }

    /// <summary>What an engagement firing for effect leaves its battery of its own ballistics (docs/battle/balance.md
    /// BL1ad): its correction is a reading of the residual bias with the rest of what's in it (in range the computer's doubt
    /// of her place, the error its instruments held on her, which the plot's mean of them still has (BL1aa-ab: 1/Σ 1/σᵢ²),
    /// and the walk's step, the officer's own doubt of his aim; in line the spots' noise through their gain),
    /// so it keeps σb²/(σb²+σe²) of it, and doubts σbσe/√(σb²+σe²) of the bias still, σb the computer's residual bias
    /// [INFERRED: no source on the RN's spotting correction kept over a shift of target].</summary>
    CarriedCorrection Learnt(int f)
    {
        ref readonly var fc = ref fcs[f];
        var c = fc.Fit.Computer;
        double weight = 0;
        foreach (ref readonly var s in sensors.AsSpan(f * MaxSensors, fc.SensorCount))
            if (s.Cuts > 0)
                weight += s.WRange;
        double held = weight > 0 ? 1 / weight : 0;
        double bR = fc.BallR, eR = Math.Sqrt(Sq(fc.PosSigmaM) + held + Sq(SpotStep(fc)) / 3) / fc.RangeM;
        double g = settings.SpotDeflectionGain, bD = fc.BallD, eD = settings.SpotDeflectionMil * FireControl.Mil * Math.Sqrt(g / (2 - g));
        static (double Keep, double Doubt) Shrink(double b, double e) =>
            b <= 0 ? (0, 0) : (b * b / (b * b + e * e), b * e / Math.Sqrt(b * b + e * e));
        var (kR, dR) = Shrink(bR, eR);
        var (kD, dD) = Shrink(bD, eD);
        return new(true, kR * fc.CorrR / fc.RangeM, kD * fc.CorrD, dR, dD);
    }

    /// <summary>The first step beyond a bound (F6): twice the error the computer expects of its own solution over the time
    /// of flight (its doubt of the target's position and rate along the line of fire, and its residual bias), never more
    /// than its own first step (the RN's 400 yd) nor under the bracket's floor.</summary>
    double FirstStep(in Fc fc)
    {
        var c = fc.Fit.Computer;
        double rate = fc.DriftMs * fc.TofS, resid = fc.ResidShare * fc.RangeM;
        double sigma = Math.Sqrt(fc.PosSigmaM * fc.PosSigmaM + rate * rate + resid * resid);
        return Math.Clamp(2 * sigma, settings.BracketFloorM, Math.Max(c.FirstStepM, settings.BracketFloorM));
    }

    /// <summary>Whether a battery's track has settled enough to open fire on (F6 item 1, "the tracker's own uncertainty
    /// below a doctrine's bound"): twice its doubt of where she'll be when the shells fall (her position and its rate
    /// over the time of flight along the line of fire) within <see cref="SimSettings.OpenDoubtSteps"/> of its first
    /// step, so the ranging finds her as soon as more readings would. The residual ballistic bias isn't in it: waiting
    /// takes none of it off, only the spots do.</summary>
    bool Settled(in Fc fc)
    {
        double rate = fc.DriftMs * fc.TofS;
        double step = Math.Max(fc.Fit.Computer.FirstStepM, settings.BracketFloorM);
        return 2 * Math.Sqrt(fc.PosSigmaM * fc.PosSigmaM + rate * rate) <= settings.OpenDoubtSteps * step;
    }

    /// <summary>The gunnery officer's worth of a salvo (docs/battle/balance.md BL1p): his chance a shell, from what he
    /// knows, in <see cref="Matchup.ShellChance"/>'s form: the target's hitting space as he sees her (her aspect, her
    /// freeboard's danger space at the fall), against his aim's doubt (a closed bracket's width, else his computer's own
    /// sigma of her position and rate over the time of flight and the residual bias; firing for effect, the walk's rocking
    /// at its step), the laying and the roll's slip
    /// (as BL1b has them, by a trained man's numbers), her turn's kick (BL1m) and the pattern. Now; steady (her turn
    /// left out); and what fire for effect could do from here, steady (his aim at the bracket's floor).</summary>
    (double Now, double Steady, double Could) Worth(in Fc fc, ShipClass cls, in ShipState ship, in ShipFrame frame, in ShipState tgt, in BatteryRow row,
        ReadOnlySpan<int> mounts, int guns, GunneryCrew crew, long now)
    {
        var table = shells.Ballistics[fc.Table];
        double h0 = Muzzle(cls, frame, mounts[0]).Length - Earth.Radius, range = Math.Max(fc.RangeM, 1);
        double e = double.IsNaN(fc.ElevationDeg) ? table.ElevationFor(range, h0) : fc.ElevationDeg;
        if (double.IsNaN(e))
            return (0, 0, 0);
        double fall = table.Fall(e, h0).FallDeg, perRad = table.RangePerDeg(e, h0) * 180 / Math.PI;
        double perMv = FireControl.PerMvPct(table, e, h0);
        var (sr, sd) = FireControl.Dispersion(table, row.PatternPct, range, e, h0, settings, perMv, perRad);
        sr = Math.Sqrt(sr * sr + Sq(settings.SalvoMvPct * perMv));   // the salvo's charges (ES7) as he knows them
        var computer = fc.Fit.Computer;
        var mount = fc.Fit.Mount;
        // his aim's doubt along the line of fire
        double aim = fc.Phase == FirePhase.Effect ? SpotStep(fc) / Math.Sqrt(3)
            : fc.Bracket.Closed ? Math.Max(fc.Bracket.HiAt(now, fc.DriftMs) - fc.Bracket.LoAt(now, fc.DriftMs), 0) / Math.Sqrt(12)
            : Math.Sqrt(fc.PosSigmaM * fc.PosSigmaM + Sq(fc.DriftMs * fc.TofS) + Sq(fc.ResidShare * range));
        double floorM = Math.Max(settings.BracketFloorM, settings.BracketFloorPatterns * 4 * sr);
        // and where his spots put it: his spotter's own error over or short of a splash (k R² / h from his height, through
        // the air), the bracket's place as good as the spots it was set by
        double spot = settings.SpotOpticK * range * range / Math.Max(fc.SpotHeightM, 1) * SightModel.HazeFactor(fc.Margin, settings.SpotHazePower, settings);
        aim = Math.Sqrt(aim * aim + spot * spot);
        double could = Math.Sqrt(Sq(floorM / Math.Sqrt(12)) + Sq(fc.DriftMs * fc.TofS) + spot * spot);
        // the laying and the roll's slip at the key (or the gear's lag following it), as BL1b lays them
        double lay = mount.LayMil * FireControl.Mil * crew.Lay;
        double tau = fc.OnRoll ? mount.RollTauS * crew.RollTiming : mount.StableVertical ? mount.RollTauS : settings.ContinuousAimTauS;
        double sea = Sq(lay * perRad) + Sq(tau * Math.Sqrt(2) * fc.Roll.RateSigmaRadS * perRad);
        // her own turn as he sees its cost (FL1): his table's own speed off hers over the flight and the plot's latency (what
        // the track takes into her motion), and a hand-followed course's in the line as it turns the bearings
        double kick = Math.Sqrt(Sq(Math.Abs(fc.OwnHatSpeed - ship.Speed) * (fc.TofS + computer.LatencyS)) +
                                Sq(double.DegreesToRadians(Wrap(ship.HeadingDeg - fc.OwnHatHeadingDeg)) * range));
        double rSteady = aim * aim + sea + sr * sr, rCould = could * could + sea + sr * sr;
        // and in line: his deflection's residual bias until a spot has corrected it, the laying, the pattern
        double defl = fc.DeflDoubtRad * range * (fc.LastSign < 0 ? 1 : 0.5);
        double lSteady = defl * defl + Sq(lay * range) + sd * sd;
        // her hitting space as he sees her: her length and beam on the line of fire by her aspect
        var tm = tgt.Handling.Class.Motion;
        var u = Earth.Forward(ship.Position, Earth.Bearing(ship.Position, tgt.Position));
        double ca = Math.Abs(Vec3d.Dot(tgt.Forward, u)), sa = Math.Sqrt(Math.Max(1 - ca * ca, 0));
        double depth = tm.LengthM * ca + tm.BeamM * sa, width = tm.LengthM * sa + tm.BeamM * ca;
        double danger = Matchup.DangerHeightM(tgt.Handling.Class, settings) / Math.Tan(double.DegreesToRadians(Math.Max(fall, 0.1)));
        return (Matchup.ShellChance(danger, depth, width, Math.Sqrt(rSteady + kick * kick), Math.Sqrt(lSteady + kick * kick)),
            Matchup.ShellChance(danger, depth, width, Math.Sqrt(rSteady), Math.Sqrt(lSteady)),
            Matchup.ShellChance(danger, depth, width, Math.Sqrt(rCould), Math.Sqrt(lSteady)));
    }

    /// <summary>The share of a battery's mounts' rounds still in them (their stowage's).</summary>
    double RoundsShare(int slot, ShipClass cls, ReadOnlySpan<int> mounts)
    {
        double left = 0, full = 0;
        for (int j = 0; j < mounts.Length; j++)
            (left, full) = (left + ships.Store.Combat.Mount(slot, mounts[j]).Rounds, full + cls.Mounts[mounts[j]].Rounds);
        return full > 0 ? left / full : 1;
    }

    static double Sq(double x) => x * x;

    /// <summary>Whether a battery fires independently (F6d): its discipline's range for it, and its guns quick-firers.</summary>
    bool Independent(FireDiscipline dis, ShipClass cls, ReadOnlySpan<int> mounts, double rangeM) =>
        dis.IndependentWithinM > 0 && rangeM < dis.IndependentWithinM && cls.Mounts[mounts[0]].ReloadS <= settings.QuickFirerReloadS;

    /// <summary>A partial salvo by the discipline, of the mounts that bear (loaded or not), as the guns it wants and the
    /// mounts it may take (int.MaxValue: no limit, a full salvo): ranging, the discipline's share of the guns, at least
    /// its minimum (enough to spot a wild shot by; all of them if fewer); firing for effect in halves, half the mounts,
    /// the larger half and the smaller in turn (five turrets: three, then two), while the smaller half still has that
    /// minimum: with fewer bearing, full salvos (a chase's four guns fired in pairs left the spotter two splashes, a
    /// straddle seldom seen, and fire for effect fell back to ranging).</summary>
    static (int Guns, int Mounts) Partial(FireDiscipline dis, FirePhase phase, int bearingGuns, int bearingMounts, int half)
    {
        if (phase == FirePhase.Ranging && dis.GunsShare < 1)
            return (Math.Min(bearingGuns, Math.Max(dis.GunsMin, (int)Math.Ceiling(dis.GunsShare * bearingGuns - 1e-9))), int.MaxValue);
        if (phase == FirePhase.Effect && dis.EffectHalves && bearingMounts > 1 && bearingGuns * (bearingMounts / 2) / bearingMounts >= dis.GunsMin)
            return (int.MaxValue, half == 0 ? (bearingMounts + 1) / 2 : bearingMounts / 2);
        return (int.MaxValue, int.MaxValue);
    }

    static double Mod(double d) => (d % 360 + 360) % 360;

    static double Wrap(double d) => d - 360 * Math.Round(d / 360);

    // ------------------------------------------------------------------ reading it

    /// <summary>An engaged battery's counts so far, for tools and tests: salvos, shells, hits, straddles, when it first
    /// hit and straddled (ms, -1 if not yet), when its track started, what it's doing and whether it's in local control;
    /// its fire discipline's phase, the salvos and shells it fired ranging, and when it first fired for effect (-1); its
    /// stable vertical's tilt off the true vertical, rad (docs/battle/fc-ladder-check.md FL7).</summary>
    public readonly record struct Tally(int Salvos, int Shells, int Hits, int Straddles, long FirstHitMs, long FirstStraddleMs, long TrackMs,
        BatteryState State, bool Local, FirePhase Phase, int RangingSalvos, int RangingShells, long EffectMs, double VerticalTiltRad = 0);

    /// <summary>The engagements' pool, for the coarse tier's watch (docs/battle/coarse.md J5): how far it's used, and
    /// engagement i's shooter and target (entities), true while it's live.</summary>
    internal int Engagements => highFc;

    internal bool Engagement(int i, out int shooter, out int target)
    {
        ref readonly var fc = ref fcs[i];
        (shooter, target) = (fc.Shooter, fc.Target);
        return fc.Alive;
    }

    /// <summary>A ship's battery's tally, if it's engaged.</summary>
    public Tally? TallyOf(int shooter, int battery)
    {
        for (int i = 0; i < highFc; i++)
            if (fcs[i].Alive && fcs[i].Shooter == shooter && fcs[i].Battery == battery)
            {
                ref readonly var fc = ref fcs[i];
                return new Tally(fc.Salvos, fc.Shells, fc.Hits, fc.Straddles, fc.FirstHitMs, fc.FirstStraddleMs, fc.Tracking ? fc.TrackMs : -1, fc.State, fc.Local,
                    fc.Phase, fc.RangingSalvos, fc.RangingShells, fc.EffectMs, Math.Sqrt(fc.SvTiltE * fc.SvTiltE + fc.SvTiltN * fc.SvTiltN));
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
                position = Ordered(fcs[i], position, own.Position, own.HeadingDeg);
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
        if (experienceSet)
            for (int s = 0; s < experience.Length; s++)
                if (experience[s] != (int)(Core.Experience.Trained * 1000))
                {
                    // a name's whole number as the crew quality's index was, so those hash as they did; between, apart
                    h.Add(s);
                    h.Add(experience[s] % 1000 == 0 ? experience[s] / 1000 : 10_000 + experience[s]);
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
            h.Add(fc.WalkStep);
            h.Add(fc.WalkN);
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

    /// <summary>The newest lines of the log a snapshot carries: the screens read the new ones each frame (by
    /// <see cref="CombatLog.Written"/>), so it holds the most a frame's sim time writes at a busy fight's speed.</summary>
    public const int CombatLogShown = 256;

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
            (v.RangeM[n], v.SigmaM[n], v.Straddles[n], v.InFlight[n], v.Misses[n], v.Sees[n]) = (fc.RangeM, fc.PosSigmaM, fc.Straddles, fc.InFlight, fc.Misses, fc.Sees);
            n++;
        }
        v.Count = n;
        FillLog(snap);
    }

    /// <summary>What of a hit on a ship not shown her enemy's watchers see: her fire, her magazine, her back broken.</summary>
    const HitEffect SeenEffects = HitEffect.Fire | HitEffect.Magazine | HitEffect.Broken;

    /// <summary>The log's newest lines the player may know (<see cref="CombatLogView"/>), at most
    /// <see cref="CombatLogShown"/>, from at most four times as many of the newest.</summary>
    void FillLog(Snapshot snap)
    {
        var view = snap.CombatLog;
        view.EnsureCapacity(CombatLogShown);
        var store = ships.Store;
        bool Shown(int entity) => store.SlotOf(entity) is int slot and >= 0 && snap.ShownSides[store[slot].Side];
        int n = 0, scan = Math.Min(log.Count, CombatLogShown * 4);
        for (int i = 0; i < scan && n < CombatLogShown; i++)
        {
            ref readonly var e = ref log.Newest(i);
            bool shooter = e.Shooter > 0 && Shown(e.Shooter), target = e.Target > 0 && Shown(e.Target);
            if (!shooter && !target)
                continue;
            view.Entries[n] = target || e.Kind is not (CombatEvent.Hit or CombatEvent.NearMiss)
                ? e : e with { Part = -1, Outcome = default, Effect = e.Effect & SeenEffects };
            view.Seq[n] = log.Written - 1 - i;
            n++;
        }
        view.Count = n;
        view.Written = log.Written;
    }
}
