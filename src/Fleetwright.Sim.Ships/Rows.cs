using Fleetwright.Shipgen;

namespace Fleetwright.Sim.Ships;

// The rows of ShipClass's tables: immutable structs of numbers and indices, nothing that can't be written as it is
// (docs/battle/plan.md, "Compiled ships on disk"). Conventions: an index that's absent is -1, a number that's absent
// is NaN, a list is a Slice into one of the class's pools (ShipClass.Links, Points, Numbers and its row pools).
// Heights are over the main deck and plans in ship-local metres (+x bow, +y starboard), as in Shipgen's hitboxes.

/// <summary>A run of a pool: Count items from Start.</summary>
public readonly record struct Slice(int Start, int Count)
{
    public int End => Start + Count;
    public static readonly Slice Empty = new(0, 0);
}

/// <summary>An axis-aligned box: the plan's extent and the heights.</summary>
public readonly record struct Box(double X0, double X1, double Y0, double Y1, double Base, double Top)
{
    public bool Contains(double x, double y, double z) => x >= X0 && x <= X1 && y >= Y0 && y <= Y1 && z >= Base && z <= Top;
}

/// <summary>A point in 3D, ship-local.</summary>
public readonly record struct P3(double X, double Y, double Z);

/// <summary>One hitbox component: what every kind has, and Detail, its row in its kind's side table (Mounts for main,
/// secondary and torpedo; Barbettes; Funnels for funnels and their uptakes; ShaftLines for shafts, alleys and
/// propellers; Rudders; Directors for director blocks; Tubes for submerged tubes), or -1.</summary>
public readonly record struct ComponentRow
{
    public ComponentKind Kind { get; init; }
    public ComponentRole Role { get; init; }
    public ComponentShape Shape { get; init; }
    /// <summary>A superstructure block's level on what it stands on (1: the lowest), else 0.</summary>
    public int Level { get; init; }
    /// <summary>The plan's extent (a mount's turret body at rest, a circle's square) and the heights.</summary>
    public Box Bounds { get; init; }
    /// <summary>A circle's or disc's centre, a mount's pivot (Z: a disc's).</summary>
    public double X { get; init; }
    public double Y { get; init; }
    public double Z { get; init; }
    /// <summary>A circle's radius, a segment's (shaft, tube).</summary>
    public double R { get; init; }
    /// <summary>A polygon's points (Points pool).</summary>
    public Slice Outline { get; init; }
    public double ArmourMm { get; init; }
    public int Material { get; init; }
    public double PlateMm { get; init; }
    public double WoodMm { get; init; }
    /// <summary>Berths in it (superstructure quarters).</summary>
    public int Crew { get; init; }
    public int BattleCrew { get; init; }
    /// <summary>The funnels a block's smoke comes from (Links pool: components).</summary>
    public Slice Smoke { get; init; }
    public int Detail { get; init; }
    /// <summary>The rangefinder in it, its base, m (a director's, a gunhouse's, a control position's hood), else 0.</summary>
    public double RangefinderM { get; init; }
    /// <summary>A mast's kind (Pole on every other part).</summary>
    public MastKind Mast { get; init; }
}

/// <summary>A gun or torpedo mount: the one table the sprite, the hit test and the state all use (a mount's index is
/// its place among the components with a turret shape, in component order, as TopView draws them).</summary>
public readonly record struct MountRow
{
    public int Component { get; init; }
    public ComponentKind Kind { get; init; }
    /// <summary>Its row in TurretTypes.</summary>
    public int Type { get; init; }
    public double CalibreMm { get; init; }
    public double CalibreLength { get; init; }
    public double X { get; init; }
    public double Y { get; init; }
    public double RestDeg { get; init; }
    public bool Rotating { get; init; }
    public bool Casemate { get; init; }
    public double BroadphaseR { get; init; }
    /// <summary>The arcs it bears through, as pairs of degrees clockwise from ahead (Numbers pool).</summary>
    public Slice Arcs { get; init; }
    public double TraverseFromDeg { get; init; }
    public double TraverseToDeg { get; init; }
    /// <summary>The turret's body in turret space: pivot at 0,0, barrels along +x (Points pool).</summary>
    public Slice Body { get; init; }
    /// <summary>Does it have turret armour (main and secondary mounts always do, even 0 mm)?</summary>
    public bool Turret { get; init; }
    public double FaceMm { get; init; }
    public double SideMm { get; init; }
    public double RearMm { get; init; }
    public double RoofMm { get; init; }
    /// <summary>The room it draws ammunition from.</summary>
    public int Magazine { get; init; }
    /// <summary>The barbette under it (a component).</summary>
    public int Barbette { get; init; }
    public int Rounds { get; init; }
    public int ReadyRounds { get; init; }
    public double ReadyT { get; init; }
    public int Torpedoes { get; init; }
    public double WarheadKg { get; init; }

    /// <summary>The gun battery it's in (Batteries), or -1 (torpedo mounts).</summary>
    public int Battery { get; init; }
    /// <summary>The rangefinder in its gunhouse, its base, m (0 none; fire-control.md F4b), and the eye it ranges through
    /// (Eyes; -1 none).</summary>
    public double RangefinderM { get; init; }
    public int RangefinderEye { get; init; }
    /// <summary>Working the guns (NaN on torpedo mounts): seconds from one round to the next per gun, training and
    /// elevating rates in degrees a second, and the highest elevation, which sets the gun's range. The designer's to
    /// give; placeholders until then (<see cref="DesignerPlaceholders"/>).</summary>
    public double ReloadS { get; init; }
    public double TrainDegS { get; init; }
    public double ElevateDegS { get; init; }
    public double MaxElevationDeg { get; init; }
    /// <summary>Seconds a round per gun the hoists bring once the mount's ready-use rounds (<see cref="ReadyRounds"/>) are
    /// fired (fire-control.md F6d), the sustained reload: a placeholder (<see cref="DesignerPlaceholders.HoistShare"/>).</summary>
    public double SupplyS { get; init; }
}

public enum TurretShape : byte
{
    Bb,
    Dp,
    Open,
    Casemate,
    Torp,
    Tube,
}

/// <summary>A mount type: the size, barrels and shape a mount's turret is made from.</summary>
public readonly record struct TurretTypeRow
{
    public TurretShape Shape { get; init; }
    public double R { get; init; }
    public int Barrels { get; init; }
    public double BarrelLen { get; init; }
    public double BarrelW { get; init; }
    public double Spacing { get; init; }
    public double CalibreMm { get; init; }
    public double CalibreLength { get; init; }
    /// <summary>How far ahead of the pivot the muzzles are, m, as drawn (Shipgen's Geometry.BarrelTip): where the
    /// flash starts.</summary>
    public double TipM { get; init; }
}

public readonly record struct BarbetteRow(int Component, int Mount, double X, double Y, double R);

/// <summary>A propellant's facts for its flash and smoke (docs/visuals/muzzle-flashes.md, "What the model takes";
/// research/vfx/muzzle-flash-smoke-research.md §3.1). A family is a named set of these (<see cref="Propellants"/>); a
/// later propellant system gives its own.</summary>
public readonly record struct PropellantRow
{
    /// <summary>Its energy, MJ/kg: 0.45 of it goes into the blast.</summary>
    public double EnergyMJkg { get; init; }
    /// <summary>The energy of its fuel-rich gas burning in air, MJ/kg: the secondary flash's.</summary>
    public double AfterburnMJkg { get; init; }
    /// <summary>Its secondary flash's propensity, logit (with the bore, the barrel, salt and the muzzle device).</summary>
    public double Flash0 { get; init; }
    /// <summary>The secondary flash's peak temperature, K: its brightness (≈ T¹⁵) and colour.</summary>
    public double SecondaryK { get; init; }
    /// <summary>Its own solid products, kg/kg: the smoke's particles.</summary>
    public double Solids { get; init; }
    /// <summary>Those solids' mass extinction, m²/g.</summary>
    public double ExtinctionM2g { get; init; }
    /// <summary>The water in its gas, the afterburn's included, kg/kg: the water fog in damp, cold air.</summary>
    public double Water { get; init; }
    /// <summary>Its smoke's tint near the muzzle, linear RGB albedo.</summary>
    public double TintR { get; init; }
    public double TintG { get; init; }
    public double TintB { get; init; }
}

/// <summary>A device on the muzzle: a hider (small arms) cuts the secondary flash, a brake adds to it.</summary>
public enum MuzzleDevice : byte
{
    None,
    Hider,
    Brake,
}

/// <summary>A gun's charge (docs/visuals/muzzle-flashes.md, "What the model takes"): its mass, kg, its propellant, the
/// mass fractions of flash-reducer salt (K₂SO₄, KNO₃), black-powder igniter and wear reducer (TiO₂, wax, talc), and the
/// gun's muzzle device. One per turret type (<see cref="ShipClass.Charges"/>); a later ammunition system gives several
/// (the night charge).</summary>
public readonly record struct ChargeRow
{
    public double MassKg { get; init; }
    public PropellantRow Propellant { get; init; }
    public double Salt { get; init; }
    public double Igniter { get; init; }
    public double Liner { get; init; }
    public MuzzleDevice Device { get; init; }
}

/// <summary>A funnel, its uptake under the deck (a component, or -1) and the boiler rooms it serves (Links: rooms).</summary>
public readonly record struct FunnelRow(int Component, int Uptake, int Pipes, Slice BoilerRooms);

/// <summary>A shaft line: the shaft, its propeller, its alley (or -1) and the engine room driving it.</summary>
public readonly record struct ShaftLineRow
{
    public int Shaft { get; init; }
    public int Propeller { get; init; }
    public int Alley { get; init; }
    public int EngineRoom { get; init; }
    public Position Position { get; init; }
    public double LeavesHullX { get; init; }
    public P3 P0 { get; init; }
    public P3 P1 { get; init; }
    public double R { get; init; }
    public double PropellerDiameterM { get; init; }
}

public readonly record struct RudderRow(int Component, double X, double Y, double AreaM2, int Steering);

/// <summary>A director: a superstructure block with role director.</summary>
public readonly record struct DirectorRow(int Component, Battery Battery, double RangefinderM, bool Radar);

// ------------------------------------------------------------------ the weapons (docs/battle/fighting.md 4.0)

public enum ShellKind : byte
{
    /// <summary>Armour-piercing: a small burster behind a base fuze with a delay.</summary>
    Ap,
    /// <summary>Common or semi-armour-piercing: more filler, a short delay.</summary>
    Common,
    /// <summary>High explosive: the most filler, a nose fuze that bursts on the first thing it meets.</summary>
    He,
}

/// <summary>A ship's splash colour (docs/visuals/splashes.md "Dye"): the dye load her heavy shells carry so she can
/// spot her own fall of shot among others' (from about 1930; the research's table). A sim fact, on the ship and carried
/// by each shell onto its splash; the colours themselves are the display's.</summary>
public enum Dye : byte
{
    None,
    Orange,
    Red,
    Pink,
    Yellow,
    Green,
    Blue,
    Black,
}

public enum Fuze : byte
{
    /// <summary>In the base, firing after a delay (FuzeDelayS) once armed.</summary>
    Base,
    /// <summary>In the nose, at once.</summary>
    Nose,
}

/// <summary>A shell's cap (docs/battle/shells-armour-damage.md P2): none, soft (Makarov's, torn off past 15-20° of
/// obliquity), a hard cap soldered on (Type 1) or with Krupp's strong solder (Type 2: about twice the plate to knock
/// off), or the IJN's break-away cap head (a hard cap that comes off on the water, and gives way past 45°).</summary>
public enum Cap : byte
{
    None,
    Soft,
    Hard,
    Hard2,
    BreakAway,
}

/// <summary>A kind of shell a gun fires: what its flight (the calibre, mass, muzzle velocity and drag form factor) and
/// its end (kind, cap, body, filler, fuze) need. Shared by id across classes, so a gun's trajectory table is built
/// once.</summary>
public readonly record struct ShellTypeRow
{
    public double CalibreMm { get; init; }
    public double MassKg { get; init; }
    public double MuzzleVelocityMs { get; init; }
    /// <summary>The drag form factor i over the standard Cd(Mach) curve (fire_control_ref.py's fitted i): blunt
    /// early shells ~1.5-1.9, long-headed later ones ~1.0.</summary>
    public double FormFactor { get; init; }
    public ShellKind Kind { get; init; }
    public double FillerKg { get; init; }
    /// <summary>The filler's explosive's TNT equivalent for the blast: W = FillerKg × FillerRe (black powder 0.55,
    /// picric 1.17, TNT 1; docs/shipgen/explosive-templates.md).</summary>
    public double FillerRe { get; init; }
    /// <summary>The filler's explosive's heats of detonation and full combustion (MJ/kg), the air its products need to
    /// burn again (m³/kg) and its gas share η: the gas pressure of a burst in a closed space (docs/battle/
    /// shells-armour-damage.md E5; damage/11 §2).</summary>
    public double FillerQDet { get; init; }
    public double FillerQComb { get; init; }
    public double FillerAirM3Kg { get; init; }
    public double FillerEta { get; init; }
    /// <summary>The filler's explosive (docs/battle/shells-armour-damage.md P3; damage/12 §4): the chance it bursts in a
    /// plate the shell just gets through (more on thicker, less on thinner: <c>Terminal.InPlateChance</c>), and the
    /// chance it detonates fully with the shell's booster (the rest go low-order or are duds).</summary>
    public double FillerInPlate { get; init; }
    public double FillerFull { get; init; }
    public Fuze Fuze { get; init; }
    public double FuzeDelayS { get; init; }
    /// <summary>A base fuze: the plate that starts it, calibres at normal, and its share from 61° (Okun's own curve
    /// between: <c>Terminal.FuzeStartMm</c>); whether thin plates in quick succession add up to start it (the two-plate
    /// fuzes, German and Japanese); its delay's scatter (σ as a share) and the share that fail (blind).</summary>
    public double FuzeStartCal { get; init; }
    public double FuzeOblique { get; init; }
    public bool FuzeTwoPlate { get; init; }
    public double FuzeScatter { get; init; }
    public double FuzeBlind { get; init; }
    /// <summary>On the water (docs/battle/shells-armour-damage.md E7; damage/15 §6.2): the fuze's time to fire once the shell
    /// meets it, ms (a nose fuze's burst depth, v·t·sin fall; a base fuze's on top of its delay), and the share of the
    /// fuzes that work that fire there.</summary>
    public double FuzeWaterMs { get; init; }
    public double FuzeWaterFires { get; init; }
    /// <summary>Its cap, and the cap's share of the mass (lost with the cap).</summary>
    public Cap Cap { get; init; }
    public double CapShare { get; init; }
    /// <summary>The body: past this obliquity on face-hardened plate of this many calibres or more it breaks up (the proof
    /// it was made to: RN 1914 15° on 0.4, Greenboy 20° on 0.67, US late 45° on 1.2), and its quality there, the
    /// multiplier on the plate it beats (armor/02 §4.6, 0.85-1.11).</summary>
    public double BreakObDeg { get; init; }
    public double BreakTD { get; init; }
    public double BodyQuality { get; init; }
    /// <summary>Its burst's pieces (docs/battle/shells-armour-damage.md E1; <see cref="Ships.Fragments"/>): null for a
    /// shell without filler. Equal when made of the same numbers.</summary>
    public Fragments? Fragments { get; init; }
    /// <summary>A diving shell (the IJN's Type 88/91): its cap head comes off on the water, leaving a flat nose that runs
    /// true under it instead of tumbling, so it keeps its speed there longer (WaterRun).</summary>
    public bool Diving => Cap == Cap.BreakAway;
}

/// <summary>A shell type a battery carries, and its share of the rounds.</summary>
public readonly record struct ShellLoad(int ShellType, double Share);

/// <summary>The magazine order (docs/battle/shells-armour-damage.md decision 7): a main battery fires AP until it's
/// spent, then common, then HE; a secondary battery HE first, then common, then AP. A battery's loads are kept in this
/// order, so a mount fires the first it has rounds of. The AI's choice by target replaces it later (decision 3).</summary>
public static class MagazineOrder
{
    /// <summary>A kind's place in a battery's order: lower fires first.</summary>
    public static int Rank(Battery battery, ShellKind kind) => battery == Battery.Main
        ? kind switch { ShellKind.Ap => 0, ShellKind.Common => 1, _ => 2 }
        : kind switch { ShellKind.He => 0, ShellKind.Common => 1, _ => 2 };
}

/// <summary>How a battery's fire is controlled, the rungs of research/simulation/fire_control_ref.py's LEVELS (0-8;
/// local control is a battery without a director, not a level). The design's choice (fighting.md decision 6); a
/// placeholder reads it from the fit until the designer offers it.</summary>
public enum FireControlLevel : byte
{
    /// <summary>0: ranges by eye (c.1890).</summary>
    Eyeball,
    /// <summary>1: an early rangefinder and stadimeter (1905).</summary>
    Rangefinder,
    /// <summary>2: Dumaresq and range clock (1910).</summary>
    Clock,
    /// <summary>3: Dreyer table and director firing (1916).</summary>
    Director,
    /// <summary>4: the interwar central table (AFCT, 1930s).</summary>
    Table,
    /// <summary>5: rangekeeper and stable vertical, radar ranging (1941-42).</summary>
    Rangekeeper,
    /// <summary>6: radar spotting (1945).</summary>
    RadarSpotting,
    /// <summary>7: auto-tracking radar (1955).</summary>
    AutoTrack,
    /// <summary>8: digital (1975).</summary>
    Digital,
}

// ------------------------------------------------------------------ fire control (docs/battle/fire-control.md F0)

/// <summary>How an instrument takes a range: by eye (a director without a rangefinder, or one whose make isn't read), a
/// coincidence rangefinder (two half-images brought into line) or a stereo one (two images fused, the range read off a
/// mark floating in depth: better in haze, fire-control.md F5c). Radar and laser come with their models (F9).</summary>
public enum InstrumentKind : byte
{
    Eye,
    Coincidence,
    Stereo,
}

/// <summary>What a rangefinder stands on, so how much it shakes (fire-control-designer.md §3.1: the stiffness that caps
/// its useful magnification): a gunhouse, the conning tower or a tower mast, a superstructure's roof, a tripod, a pole
/// mast's top, a cage or a lattice mast's.</summary>
public enum InstrumentSupport : byte
{
    Deck,
    Turret,
    Tower,
    Tripod,
    Pole,
    Cage,
    Lattice,
}

/// <summary>An instrument a fire-control unit reads from: the unit, the class's eye it looks through (its blind sectors),
/// its component, where it stands (ship-local x, y), its height over the waterline at rest, its kind and its base, m (0
/// by eye). Its optics' make is the battery's fit (<see cref="OpticsRow"/>).</summary>
public readonly record struct InstrumentRow(int Unit, int Eye, int Component, double X, double Y, double HeightM, InstrumentKind Kind, double BaseM);

/// <summary>A fire-control unit: a control position whose instruments and computer hold one target at a time, and the
/// kind of battery it serves: a director (its index; a unit's index is its director's), or a control position's
/// rangefinder with no director (Director -1: the guns lay by their own sights on its ranges; fire-control.md F4b), those
/// after the directors; its instruments (Links: instrument indices) highest first.</summary>
public readonly record struct FcUnitRow(int Director, Battery Battery, Slice Instruments);

/// <summary>The instruments' make on a battery's fit: their magnification (0: the instruments can't be read, so a
/// rangefinder ranges by eye), the readings a minute each takes, a trained rangetaker's acuity through them, arcseconds,
/// and the longest base their tube keeps true, m, past which a base delivers as the flex power of its length
/// (fire-control-designer.md §3.1: B_eff = rigid·(B/rigid)^flex); their kind (coincidence or stereo), the highest
/// magnification their glass gives a full image at (0: no limit), whether the glass is coated, and whether the mounting
/// is gyro-stabilised (fire-control.md F5).</summary>
public readonly record struct OpticsRow(double Magnification, double ReadingsPerMin, double DeltaArcsec, double RigidBaseM, double FlexExp,
    InstrumentKind Kind = InstrumentKind.Coincidence, double MaxMagnification = 0, bool Coated = false, bool Gyro = false)
{
    /// <summary>Whether a rangefinder of this make is read at all.</summary>
    public bool Reads => Magnification > 0;
}

/// <summary>How a fire-control computer keeps its target (docs/battle/fire-control.md F2; fire_control_ref.py's trackers):
/// each fails where its machine failed.</summary>
public enum TrackerKind : byte
{
    /// <summary>A sailor's head: ranges by sight, the target's course and speed guessed from her look and led by that
    /// guess, looked at again now and then. It never learns a rate. The worst computer there is.</summary>
    Judgement,
    /// <summary>Constant range and bearing rates from own ship (Dumaresq and Vickers clock, Dreyer table, the Japanese
    /// plot): it drifts on a crossing target by geometry, and unless helm-free it re-learns its rates after own ship's
    /// turns and speed changes.</summary>
    RateClock,
    /// <summary>The target's true course and speed on a straight line (Argo, AFCT, Ford, Mk 1A): geometry exact until
    /// the target turns.</summary>
    TrueCourse,
    /// <summary>A true course plus a decaying target acceleration (the 1950s curved-course predictors).</summary>
    Acceleration,
    /// <summary>The target's speed, heading and rate of turn (digital filters; the AFCT's turn setting): follows a
    /// steady circle.</summary>
    TurnRate,
}

/// <summary>A fire-control computer (research/simulation/fire_control_ref.py FCLevel): its tracker and whether own
/// ship's course and speed are fed to it (helm-free: a rate clock's rates corrected at once; the others work in true
/// motion and need no feed); its latency, s, and the target acceleration its track allows (q, m²/s³); the clock's limits
/// (range rate, m/s, and bearing rate, °/s; 0 none); the curved-course predictor's acceleration memory, s, and its
/// acceleration (1 sigma, m/s²); the turn filter's turn-rate noise (rad²/s³) and speed noise (m²/s³: a ship loses way in
/// a hard turn, a fifth of her speed in a minute); whether it opens up on a manoeuvre (two large
/// surprises in a row: the Mk 1A's "target just turned"); how often a judgement looks at the target again, s; the eye's
/// first guess at the target's speed and course it starts from (1 sigma); the residual ballistic bias before spotting, %
/// of range, and in deflection, mil; the extra error while own ship turns, m; the first ladder step, m; the
/// salvo-to-salvo error in range, % of range, that spotting can't remove (the propellant's share and the computer's own
/// wander, one number until they're split). A tech block's (<see cref="FireControlBlocks"/>).</summary>
public readonly record struct ComputerRow
{
    public TrackerKind Tracker { get; init; }
    public bool HelmFree { get; init; }
    public double MaxRangeRateMs { get; init; }
    public double MaxBearingRateDegS { get; init; }
    public double AccelTauS { get; init; }
    public double AccelSigmaMs2 { get; init; }
    public double TurnQ { get; init; }
    public double SpeedQ { get; init; }
    public bool Detect { get; init; }
    public double RejudgeS { get; init; }
    public double LatencyS { get; init; }
    public double Q { get; init; }
    public double PriorKn { get; init; }
    public double PriorDeg { get; init; }
    public double ResidPct { get; init; }
    public double DeflBiasMil { get; init; }
    public double OwnTurnKickM { get; init; }
    public double FirstStepM { get; init; }
    public double SalvoPct { get; init; }
}

/// <summary>How a battery's guns are laid: whether a director fires them (its laying error common to the salvo; else
/// each gun lays itself and its error is dispersion), that laying error, mil, and the salvo-to-salvo wander in
/// deflection that spotting can't remove, mil.</summary>
public readonly record struct MountTechRow(bool DirectorFiring, double LayMil, double SalvoDeflMil);

/// <summary>A battery's fire-control fit: its instruments' make, its computer and its mounts' laying, from the tech
/// blocks its fit names (<see cref="FireControlBlocks"/>; a preset's: <see cref="FireControlPresets"/>).</summary>
public readonly record struct FireControlFit(OpticsRow Optics, ComputerRow Computer, MountTechRow Mount);

/// <summary>A gun battery: the mounts of one kind (main or secondary) firing one gun (calibre and length), so one
/// fall of shot to spot; its shells are a loadout's (<see cref="Loadout"/>). Its directors are the ship's directors of its kind;
/// none means local control (each mount aims for itself).</summary>
public readonly record struct BatteryRow
{
    public Battery Kind { get; init; }
    /// <summary>The side its mounts stand on: a secondary gun's port, starboard and centreline mounts are three batteries,
    /// each with its own fire control and target; a main battery is one whatever the side (None).</summary>
    public Side Side { get; init; }
    public double CalibreMm { get; init; }
    public double CalibreLength { get; init; }
    /// <summary>Its mounts (Links: mount indices) and their barrels.</summary>
    public Slice Mounts { get; init; }
    public int Guns { get; init; }
    /// <summary>Its directors (Links: director indices), best first; empty: local control.</summary>
    public Slice Directors { get; init; }
    /// <summary>Its fire-control units (Links: unit indices), in its directors' order.</summary>
    public Slice Units { get; init; }
    /// <summary>The preset its fit was made from: a label (fire-control.md decision 1).</summary>
    public FireControlLevel Level { get; init; }
    /// <summary>Its fire control's fit under its directors; local control fires at <see cref="FireControlPresets.Local"/>.</summary>
    public FireControlFit Fit { get; init; }
    /// <summary>The make of its gunhouses' rangefinders: its fit's optics, directors or not (fire-control.md F4b: a turret
    /// in local control ranges with its own).</summary>
    public OpticsRow TurretOptics { get; init; }
    /// <summary>The sights its guns lay by when no director fires them (local control): its fit's mount when that lays by
    /// sights, else the placeholder's (<see cref="DesignerPlaceholders.Sights"/>).</summary>
    public MountTechRow Sights { get; init; }

    /// <summary>The gun's full-salvo pattern, its 100% spread in range as a percentage of the range (fire_control_ref.py
    /// Gun.pattern_pct): the shells' own dispersion, before any fire control. A placeholder until the designer gives it
    /// (<see cref="DesignerPlaceholders.Pattern"/>).</summary>
    public double PatternPct { get; init; }

    public bool Local => Directors.Count == 0;

    /// <summary>Its guns for a label: "6 x 152mm", a secondary side's with P, S or C after it.</summary>
    public string Label => FormattableString.Invariant($"{Guns} x {CalibreMm:0}mm") + Side switch
    {
        Side.Port => " P",
        Side.Starboard => " S",
        Side.Centre => " C",
        _ => "",
    };
}

/// <summary>The hull's own plating (Shipgen's HullPlates): the side shell amidships (MidX0..MidX1) and at the ends,
/// the strength deck likewise, the lower decks, bulkheads and inner bottom, the superstructure and the control
/// positions; mm, and the material.</summary>
public readonly record struct PlatingRow
{
    public int Material { get; init; }
    public double ShellMm { get; init; }
    public double ShellEndMm { get; init; }
    public double StrengthDeckMm { get; init; }
    public double StrengthDeckEndMm { get; init; }
    public double MidX0 { get; init; }
    public double MidX1 { get; init; }
    public double DeckMm { get; init; }
    public double BulkheadMm { get; init; }
    public double InnerBottomMm { get; init; }
    public double SuperstructureMm { get; init; }
    public double ControlMm { get; init; }
    public double DeckWoodMm { get; init; }
}

/// <summary>A submerged torpedo tube, from P0 (inboard) to P1 (the muzzle), in Room.</summary>
public readonly record struct TubeRow
{
    public int Component { get; init; }
    public int Room { get; init; }
    public Position Position { get; init; }
    public double CalibreMm { get; init; }
    public int Torpedoes { get; init; }
    public double WarheadKg { get; init; }
    public double RestDeg { get; init; }
    public bool Rotating { get; init; }
    public Slice Arcs { get; init; }
    public P3 P0 { get; init; }
    public P3 P1 { get; init; }
    public double R { get; init; }
}

/// <summary>A torpedo (docs/battle/fighting.md 4.6): its calibre, its charge (kg of TNT's worth: what the explosion does),
/// and its two settings of speed and range, the fast one first. The designer's to give; placeholders until then
/// (<see cref="DesignerPlaceholders.Torpedo"/>).</summary>
public readonly record struct TorpedoRow
{
    public double CalibreMm { get; init; }
    public double ChargeKg { get; init; }
    public double FastKn { get; init; }
    public double FastRangeM { get; init; }
    public double SlowKn { get; init; }
    public double SlowRangeM { get; init; }
}

/// <summary>Where a torpedo comes from: a torpedo mount on deck or a submerged tube.</summary>
public enum LauncherKind : byte
{
    Mount,
    Tube,
}

/// <summary>A torpedo launcher (docs/battle/fighting.md 4.6): a torpedo mount (Index: Mounts) or a submerged tube (Index:
/// Tubes), as one table. Where its torpedoes enter the water (ship frame), the arcs they leave through (pairs of degrees
/// clockwise from ahead, Numbers), its bearing when it doesn't train, its tubes (fired at once), the torpedoes it carries
/// (those in its tubes included), how long its tubes take to load again, and its torpedo (Torpedoes).</summary>
public readonly record struct LauncherRow
{
    public LauncherKind Kind { get; init; }
    public int Index { get; init; }
    public int Component { get; init; }
    public double X { get; init; }
    public double Y { get; init; }
    public double Z { get; init; }
    public Slice Arcs { get; init; }
    public bool Trains { get; init; }
    public double RestDeg { get; init; }
    public int Tubes { get; init; }
    public int Torpedoes { get; init; }
    public double ReloadS { get; init; }
    public int Torpedo { get; init; }
}

/// <summary>A watertight cell: one section x tier x band.</summary>
public readonly record struct CellRow
{
    public int Section { get; init; }
    public int Tier { get; init; }
    public Band Band { get; init; }
    public Box Box { get; init; }
    public bool BelowWaterline { get; init; }
    public double VolumeM3 { get; init; }
    public double Permeability { get; init; }
    public bool Citadel { get; init; }
    public double BeltMm { get; init; }
    public int BeltMaterial { get; init; }
    public double TdsM { get; init; }
    public int Crew { get; init; }
    public int BattleCrew { get; init; }
    public int Room { get; init; }
    /// <summary>The rooms that share it besides Room (Links).</summary>
    public Slice Also { get; init; }
    /// <summary>Its neighbours (the Neighbours pool): each edge is listed from both cells.</summary>
    public Slice Neighbours { get; init; }
    /// <summary>The armour decks over it, top first (the ArmourAbove pool).</summary>
    public Slice ArmourAbove { get; init; }
    /// <summary>The shafts and alleys through it (Links: components).</summary>
    public Slice Through { get; init; }
}

/// <summary>An edge of the cell graph: the cell on the other side and what joins them (a deck or bulkhead index; -1
/// when open).</summary>
public readonly record struct Neighbour(int Cell, JoinKind Join, int Through);

public readonly record struct ArmourLayer(double Mm, int Material);

/// <summary>A material as plates of it resist shells (docs/battle/shells-armour-damage.md P1b; armor/00 §4.2): q, the
/// share of the formula's plate (WWII STS) it's worth against an 8" capped shell; face-hardened plate's calibre scaling
/// k and its face (share, and the thinnest plate made so, mm); homogeneous plate's elongation, %. Absent: no scaling
/// (k 0), not face-hardened (FaceMinMm NaN), elongation 25.</summary>
/// <summary>A material as the terminal reads it: q and its rules (<see cref="Against"/>), the face, and where its back gives
/// to a shell it stops (SpallU, HoledU: u = V/V_L; docs/battle/shells-armour-damage.md P4).</summary>
public readonly record struct MaterialRow(double Q, double QScale, double FaceShare, double FaceMinMm, double ElongationPct, bool FaceShatters,
    double SpallU, double HoledU)
{
    /// <summary>A plate with no material named: the formula's own (q 1), homogeneous.</summary>
    public static readonly MaterialRow None = new(1, 0, double.NaN, double.NaN, 25, true, 0.90, 1.00);

    const double EightIn = 8 * 25.4;

    public bool FaceHardened => !double.IsNaN(FaceMinMm);

    /// <summary>How fragments see it (THOR's class, E1): mild unless given.</summary>
    public FragmentClass Fragments { get; init; }

    /// <summary>E3: the share of its spall that leaves as one big piece once the severity is 0.5 or more.</summary>
    public double BigShare { get; init; } = 0.5;

    /// <summary>Its density, t/m³ (steel's 7.85 unless given).</summary>
    public double Density { get; init; } = Steel;

    /// <summary>A plate of it mm thick as steel of the same weight, mm: what starts a fuze or knocks a cap off is the
    /// plate's mass in the way (a backing of teak or cement is a thin plate to them; P7).</summary>
    public double SteelMm(double mm) => mm * Density / Steel;

    const double Steel = 7.85;

    public static MaterialRow Of(MaterialInput m) => new(m.Q ?? 1, m.QScale ?? 0, m.FaceHardened?.Share ?? double.NaN,
        m.FaceHardened is { } f ? f.MinMm ?? 0 : double.NaN, m.ElongationPct ?? 25, m.FaceHardened?.Shatters ?? true, m.Back?.SpallU ?? 0.90,
        m.Back?.HoledU ?? 1.00) { Density = m.DensityTPerM3 ?? Steel, Fragments = FragmentsOf(m.Fragments), BigShare = m.Back?.Big ?? 0.5 };

    static FragmentClass FragmentsOf(string? c) => c switch
    {
        "ductile" => FragmentClass.Ductile,
        "fh" => FragmentClass.Fh,
        "brittle" => FragmentClass.Brittle,
        _ => FragmentClass.Mild,
    };

    /// <summary>A plate of it mm thick at an obliquity as the mild steel that stops fragments as it does (E1).</summary>
    public double FragmentMildMm(double mm, double obliquityDeg = 0) => Ships.Fragments.MildMm(SteelMm(mm), Fragments, obliquityDeg);

    /// <summary>The plate's worth as a share of its thickness against a shell of a calibre (00 §4.2's rules, all by the
    /// material's numbers): face-hardened plate q·(D/8")^−k past 8" (held at q below), and no face-hardened advantage
    /// (×0.9) under max(the thinnest made, 0.3 calibres); homogeneous plate under 25% elongation loses to shells over
    /// 8" by Okun's M79 penalty, 1 − (1 − √(El/25))·min(D − 8", 8")/8".</summary>
    public double Against(double tMm, double calibreMm)
    {
        double q = Q;
        if (FaceHardened)
        {
            if (calibreMm > EightIn && QScale != 0)
                q *= Math.Pow(calibreMm / EightIn, -QScale);
            if (tMm < Math.Max(FaceMinMm, 0.3 * calibreMm))
                q *= 0.9;
        }
        else if (ElongationPct < 25 && calibreMm > EightIn)
            q *= 1 - (1 - Math.Sqrt(ElongationPct / 25)) * Math.Min((calibreMm - EightIn) / EightIn, 1);
        return q;
    }
}

public readonly record struct RoomRow
{
    public RoomKind Kind { get; init; }
    public Fuel Fuel { get; init; }
    /// <summary>What it holds at the start (fuel, ammunition), tonnes; NaN when not given.</summary>
    public double Tonnes { get; init; }
    /// <summary>The mount it serves alone (a component), or -1.</summary>
    public int Mount { get; init; }
    public bool Shared { get; init; }
    public int Crew { get; init; }
    public int BattleCrew { get; init; }
    public double VolumeM3 { get; init; }
    public double X0 { get; init; }
    public double X1 { get; init; }
    public double Base { get; init; }
    public double Top { get; init; }
    /// <summary>Links: cells, then the components it serves (mounts, tubes, shafts, rudders).</summary>
    public Slice Cells { get; init; }
    public Slice Mounts { get; init; }
    public Slice Tubes { get; init; }
    public Slice Shafts { get; init; }
    public Slice Rudders { get; init; }
}

/// <summary>A transverse bulkhead (X) or a longitudinal one (Section, Side, Y, X0..X1).</summary>
public readonly record struct BulkheadRow
{
    public BulkheadKind Kind { get; init; }
    public double X { get; init; }
    public int Section { get; init; }
    public Side Side { get; init; }
    public double Y { get; init; }
    public double X0 { get; init; }
    public double X1 { get; init; }
    public double Base { get; init; }
    public double Top { get; init; }
    public double PlateMm { get; init; }
    public double ArmourMm { get; init; }
    public double ArmourBottom { get; init; }
    public double ArmourTop { get; init; }
    public double ArmourLowerMm { get; init; }
    public double ArmourLowerTop { get; init; }
    public int ArmourMaterial { get; init; }
    /// <summary>The lowest strake's plate, up to PlateLowTop (NaN: one plate all the way).</summary>
    public double PlateLowMm { get; init; }
    public double PlateLowTop { get; init; }

    /// <summary>The plate at a height.</summary>
    public double PlateAt(double z) => z < PlateLowTop ? PlateLowMm : PlateMm;
}

public readonly record struct DeckRow
{
    public DeckKind Kind { get; init; }
    /// <summary>Shipgen's deck number: 0 the main deck, counting up below it, negative for raised decks (and 0 when it
    /// has none: the inner bottom).</summary>
    public int Number { get; init; }
    public double Z { get; init; }
    /// <summary>Where it runs, as x pairs (Numbers); empty: the whole hull.</summary>
    public Slice Spans { get; init; }
    public double PlateMm { get; init; }
    public double PlateEndMm { get; init; }
    public double WoodMm { get; init; }
    public double ArmourMm { get; init; }
    public double X0 { get; init; }
    public double X1 { get; init; }
    public int Material { get; init; }
    /// <summary>Its armour plates (the DeckPlates pool).</summary>
    public Slice Plates { get; init; }
}

public readonly record struct DeckPlate(double ArmourMm, double X0, double X1, int Material);

/// <summary>The space between two decks: Floor is -1 for the bottom tier (the keel).</summary>
public readonly record struct TierRow(double Base, double Top, bool BelowWaterline, double Submerged, int Floor, int Ceiling, Slice Spans);

public readonly record struct SectionRow(double X0, double X1);

// ------------------------------------------------------------------ the armour report

public enum StrakeKind : byte
{
    Belt,
    End,
    Upper,
    Box,
    /// <summary>The belt over the magazines, in place of the belt there (protection-plan.md H5).</summary>
    Zone,
}

/// <summary>Side armour: the belt or a strake, on the hull's side over X0..X1 and Bottom..Top.</summary>
public readonly record struct SidePlateRow
{
    public StrakeKind Kind { get; init; }
    public Extent Extent { get; init; }
    public double ThicknessMm { get; init; }
    public double X0 { get; init; }
    public double X1 { get; init; }
    public double Bottom { get; init; }
    public double Top { get; init; }
    public int Material { get; init; }
    /// <summary>The belt's thickness at its lower edge, tapering from TaperFrom down (NaN: no taper).</summary>
    public double BottomMm { get; init; }
    public double TaperFrom { get; init; }
    /// <summary>An end strake's thickness at the ship's end (NaN: none).</summary>
    public double TipMm { get; init; }
    /// <summary>The belt's (and its zones') lean off the upright, its top outboard, degrees (protection-plan.md H6).</summary>
    public double InclineDeg { get; init; }
    /// <summary>Its backing behind it (docs/battle/shells-armour-damage.md P7): mm (0: none) of a material.</summary>
    public double BackingMm { get; init; }
    public int BackingMaterial { get; init; }
}

/// <summary>An armoured transverse bulkhead: ThicknessMm from Top down, LowerMm under LowerTop.</summary>
/// <summary>An armoured transverse bulkhead's plane at X; W: as wide as that about the centreline (NaN: the hull's width).</summary>
public readonly record struct ArmourBulkheadRow(double X, double ThicknessMm, double Bottom, double Top, int Material, double LowerMm, double LowerTop,
    double W = double.NaN);

/// <summary>An armour deck's plate: Deck (an index) at Z over X0..X1.</summary>
public readonly record struct ArmourDeckRow(int Deck, double ThicknessMm, Extent Extent, double X0, double X1, double Z, bool Main, bool Roof, int Material,
    double W = double.NaN)
{
    /// <summary>A deck with slopes (protection-plan.md H6): the flat stops SlopeReach in from the side, its half-breadth
    /// taken at SlopeFoot (NaN: flat to the side).</summary>
    public double SlopeReach { get; init; } = double.NaN;
    public double SlopeFoot { get; init; } = double.NaN;

    /// <summary>Whether it covers (x, y): its stretch, and its width when it has one, and its flat's when it has slopes.</summary>
    public bool Covers(double x, double y, HullField hull) =>
        x >= X0 && x <= X1 && !(Math.Abs(y) > W / 2) && (double.IsNaN(SlopeReach) || Math.Abs(y) <= hull.HalfWidth(x, SlopeFoot) - SlopeReach);
}

/// <summary>An inclined longitudinal plate each side (protection-plan.md H6): a deck's slope, an internal belt. Over X0..X1 from Z0 up to
/// Z1, U0 in from the side at Z0 and U1 at Z1, linear between, the side's half-breadth taken at SideZ; ThicknessMm thick,
/// BottomMm at Z0 tapering from TaperFrom down (NaN: one thickness).</summary>
public readonly record struct InclinedRow
{
    public InclinedKind Kind { get; init; }
    public double ThicknessMm { get; init; }
    public double X0 { get; init; }
    public double X1 { get; init; }
    public double Z0 { get; init; }
    public double Z1 { get; init; }
    public double U0 { get; init; }
    public double U1 { get; init; }
    public double SideZ { get; init; }
    public int Material { get; init; }
    public double BottomMm { get; init; }
    public double TaperFrom { get; init; }

    /// <summary>How far in from the side it stands at a height.</summary>
    public double InboardAt(double z) => U0 + (z - Z0) * (U1 - U0) / (Z1 - Z0);

    /// <summary>Its thickness at a height: tapering below TaperFrom to BottomMm at Z0.</summary>
    public double MmAt(double z) => !double.IsNaN(BottomMm) && !double.IsNaN(TaperFrom) && z < TaperFrom && TaperFrom > Z0
        ? BottomMm + (ThicknessMm - BottomMm) * Math.Clamp((z - Z0) / (TaperFrom - Z0), 0, 1)
        : ThicknessMm;

    /// <summary>How far out from the centreline the plate stands at (x, z), either side (negative: none there, a fine end
    /// narrower than its reach).</summary>
    public double YAt(HullField hull, double x, double z) => hull.HalfWidth(x, SideZ) - InboardAt(z);
}

public enum InclinedKind : byte
{
    /// <summary>A deck's slope.</summary>
    Slope,
    /// <summary>An internal belt, and its zones over the magazines.</summary>
    Belt,
    Zone,
}

// ------------------------------------------------------------------ the hull

/// <summary>The hull's heights: keel, waterline (full load), the armour deck (NaN: none), and the knuckle where the
/// sides lean in (NaN: wall-sided).</summary>
public readonly record struct VerticalRow(double Keel, double Waterline, double ArmourDeck, double Draught, double Depth, double Freeboard, double Knuckle);

/// <summary>The full-load hydrostatics, to settle, trim and heel a flooded ship by added weight.</summary>
public readonly record struct HydrostaticsRow
{
    public double DisplacementT { get; init; }
    public double VolumeM3 { get; init; }
    public double WaterplaneM2 { get; init; }
    public double Lcf { get; init; }
    public double Lcg { get; init; }
    public double Lcb { get; init; }
    public double Kg { get; init; }
    public double Kb { get; init; }
    public double GmT { get; init; }
    public double GmL { get; init; }
    public double ITM4 { get; init; }
    public double ILM4 { get; init; }
    public double TpcT { get; init; }
    public double MctTm { get; init; }
}

/// <summary>What moving needs (step 2.2 builds turning and acceleration from these, its numbers in SimSettings).</summary>
public readonly record struct MotionFacts
{
    public double LengthM { get; init; }
    public double BeamM { get; init; }
    public double DraughtM { get; init; }
    public double BlockCoefficient { get; init; }
    public double FullDisplacementT { get; init; }
    public double StandardDisplacementT { get; init; }
    /// <summary>The designed speed at full load, knots, at PowerShp.</summary>
    public double MaxSpeedKn { get; init; }
    public double PowerShp { get; init; }
    /// <summary>The plant's rating, what it holds continuously, and the most it makes for a while, as a multiple of the
    /// rating (1.12: 112%).</summary>
    public double RatedShp { get; init; }
    public double ContinuousShp { get; init; }
    public double OverloadMax { get; init; }
    public int Shafts { get; init; }
    public int Rudders { get; init; }
    public double RudderAreaM2 { get; init; }
    public double GmFullM { get; init; }
    public double RollPeriodS { get; init; }

    /// <summary>The tactical diameter at full speed and hard over, metres. The designer's to give; until it does, a
    /// placeholder (<see cref="DesignerPlaceholders"/>).</summary>
    public double TacticalDiameterM { get; init; }

    /// <summary>The steering gear: how fast it puts the rudder over, degrees a second, and how far, degrees. The
    /// designer's to give; placeholders until then.</summary>
    public double RudderRateDegS { get; init; }
    public double HardOverDeg { get; init; }

    /// <summary>The plant: seconds from no power to full, and from full to none (the throttle shut: far quicker than
    /// raising steam). The designer's to give (it's the boilers and the kind of plant); placeholders until then.</summary>
    public double PowerRampS { get; init; }
    public double PowerCutS { get; init; }
}
