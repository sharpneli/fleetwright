using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>What struck a ship.</summary>
public enum HitSource : byte
{
    Shell,
    Torpedo,
    Mine,
    Collision,
    /// <summary>A hit sampled by coarse fire (docs/battle/fighting.md 4.9).</summary>
    Coarse,
    /// <summary>A shell that missed, bursting in the water close by (docs/battle/shells-armour-damage.md E7): the hit's
    /// point is the burst's, outside the hull.</summary>
    NearMiss,
}

/// <summary>A hit (docs/battle/fighting.md 4.3a; the architecture's "damage takes a hit, not a shell flight"): the ship
/// struck (an entity), when, where on it and which way the shell was going, both in the ship's own frame (the hitboxes':
/// +x bow, +y starboard, heights over the main deck), its striking speed, what struck it, the shell (or the torpedo),
/// and who fired (an entity, and the battery; a torpedo's launcher). Torpedoes (4.6), mines, collisions and coarse fire's
/// samples come in as more hits.</summary>
public readonly record struct Hit
{
    public int Ship { get; init; }
    public long Ms { get; init; }
    public P3 Point { get; init; }
    /// <summary>A unit vector along the shell's flight.</summary>
    public P3 Direction { get; init; }
    public double SpeedMs { get; init; }
    /// <summary>How far the shell ran under the water before it met the hull, m (0: it struck in the air), its
    /// <see cref="SpeedMs"/> where it met the water (<see cref="WaterRun"/>). Its own path starts just short of the hull,
    /// so only the shell's flight knows it.</summary>
    public double WaterRunM { get; init; }
    public HitSource Source { get; init; }
    public ShellTypeRow Shell { get; init; }
    /// <summary>A torpedo's hit (<see cref="HitSource.Torpedo"/>): the torpedo, its charge and calibre.</summary>
    public TorpedoRow Torpedo { get; init; }
    /// <summary>A near miss's burst (<see cref="HitSource.NearMiss"/>): its filler's force, 1 full, under 1 low-order.</summary>
    public double Force { get; init; }
    /// <summary>The shell's type in the shooter's class (for the log), or -1.</summary>
    public int ShellType { get; init; }
    public int Shooter { get; init; }
    public int Battery { get; init; }
}

/// <summary>A part's state (a component or a room): working, damaged (working, a room at half), out of action, or
/// wrecked. Nothing repairs yet (repair is fighting.md 4.5's), so a state only gets worse.</summary>
public enum PartState : byte
{
    Ok,
    Damaged,
    Disabled,
    Destroyed,
}

/// <summary>What else is wrong with a part.</summary>
[Flags]
public enum PartFlags : byte
{
    None = 0,
    /// <summary>A mount that can't train (a heavy hit on its turret or barbette that didn't get in).</summary>
    Jammed = 1,
    /// <summary>On fire (<see cref="Fires"/>).</summary>
    Burning = 2,
    /// <summary>Out for a while (its crew stunned, its power tripped): it gives nothing until
    /// <see cref="ShipCondition.DownUntil"/> (docs/battle/shells-armour-damage.md E4).</summary>
    Down = 4,
}

/// <summary>What put a node down for a while (<see cref="ShipCondition.DownWhy"/>), for the benches: a crew stunned, a
/// heavy hit's shock tripping the power round it or cascading through the ship (E4), a near miss's (E7) round it or
/// through the ship, the ship's own main battery tripping its breakers, a room's feed casualty or smoke.</summary>
public enum DownCause : byte
{
    None,
    Stunned,
    HitShock,
    HitCascade,
    NearMiss,
    NearMissCascade,
    OwnGuns,
    Feed,
    Smoke,
}

/// <summary>What a hole in the side is to damage control (docs/battle/damage-control.md DC2; 09 §4): sprung plating (seams
/// and rivets: caulked), a hole a plug stops (≤ 150 cm²), one a patch covers (≤ 0.3 m²), one only a collision mat from
/// outside reaches (≤ 5 m², near the waterline, the ship slow), or beyond repair at sea.</summary>
public enum HoleClass : byte
{
    Seam,
    Plug,
    Patch,
    Mat,
    Beyond,
}

/// <summary>An edge's marks for the flooding and damage control (docs/battle/damage-control.md DC1): a watertight door
/// in it left open, its door jammed by a load, it gave way under the head; and damage control's work on it (DC3).</summary>
[Flags]
public enum JoinMark : byte
{
    None = 0,
    DoorOpen = 1,
    DoorJammed = 2,
    Collapsed = 4,
    Caulked = 8,
    Redogged = 16,
    Shored = 32,
    ShoredTwice = 64,
    /// <summary>A deck gone under the sea with its hatches battened down (DC9: the Lützow rule's opening, closed).</summary>
    Battened = 128,
}

/// <summary>What hits that held left a mount (docs/battle/shells-armour-damage.md E4; damage/13 §1): jammed in train (its
/// component's <see cref="PartFlags.Jammed"/>, by a cause) until a time, stiff until one, guns out until one, its sights
/// gone. Times are world ms; 0 none, <see cref="NonPen.Battle"/> the rest of the battle.</summary>
public struct MountHarm
{
    public JamCause Jam;
    public long JamUntil, StiffUntil, GunUntil;
    public int GunsOut;
    public bool Sights;
}

/// <summary>A ship's steering: answering the helm, its rudder jammed at an angle (it circles), or the helm lost (the
/// rudder trails amidships).</summary>
public enum Steering : byte
{
    Free,
    Jammed,
    Lost,
    /// <summary>Steered by hand or the steam gear aft, rigged by a party (docs/battle/damage-control.md DC7): the rudder
    /// answers at a share of its rate.</summary>
    Hand,
    /// <summary>Steered by her engines (DC7): a share of hard over about the rudder's jam angle (amidships when it trails).</summary>
    Engines,
}

/// <summary>Why her steering went (DC7): the gear (the steering room, its power: damage control can rig it by hand, and
/// it answers again once the gear does), or the rudder itself (beyond repair at sea: steered by engines at best).</summary>
public enum SteerCause : byte
{
    None,
    Gear,
    Rudder,
}

/// <summary>Whether a ship is afloat or going down (its magazine gone, foundered or capsized: <see cref="LossKind"/>).
/// A ship settling and listing is still afloat until its buoyancy or stability gives out.</summary>
public enum ShipStatus : byte
{
    Afloat,
    GoingDown,
}

/// <summary>A ship's condition (docs/battle/fighting.md 4.3a, 4.3b): there are no hit points anywhere. Each part damage
/// reaches (the class's nodes: its components, then its rooms) has a state and flags; each watertight cell its water,
/// the area of its holes to the sea (and their mean height and side, ship frame), whether it burns and whether its
/// bulkheads are torn; the ship its crew lost, its status, the water aboard and the attitude and GM it settles to. What
/// it can still do (<see cref="Functions"/>) is worked out from these when they change: which mounts, directors and
/// eyes work, the power it can make and its steering. Allocated at spawn, sized by the class.</summary>
public sealed class ShipCondition
{
    public ShipClass Class { get; private set; } = null!;
    public PartState[] State = [];
    public PartFlags[] Flags = [];
    public double[] WaterM3 = [], BreachM2 = [], BreachY = [], BreachZ = [];
    /// <summary>Each cell's holes by class (docs/battle/damage-control.md DC2; by cell × <see cref="HoleClass"/>): the area
    /// and count still open, their mean height (ship frame), the area of those damage control has worked on and the share
    /// of it that still leaks (1 until worked on). <see cref="BreachM2"/> is their sum as the sea sees it: open + worked ×
    /// left.</summary>
    public double[] HoleOpenM2 = [], HoleZ = [], HoleDoneM2 = [], HoleLeft = [];
    public int[] HoleOpenN = [];
    public const int HoleClasses = 5;
    /// <summary>What each room stows stands to this height in the ship's frame (NaN: nothing; docs/battle/shell-contents.md
    /// C2-C3): her oil, coal and feed water, made from her side's fuel share (<see cref="LevelsFuel"/>, NaN before) when she
    /// first needs them. Nothing is burnt in a battle (hours, a few percent).</summary>
    public double[] Level = [];
    public double LevelsFuel = double.NaN;
    public bool[] CellFire = [], CellOpened = [];
    /// <summary>Each fire space's fire (the class's FireSpaces; docs/battle/fire.md): <see cref="CellFire"/> and a part's
    /// <see cref="PartFlags.Burning"/> show which burn.</summary>
    public BurnState[] Fire = [];
    /// <summary>The water on each fire space now (docs/battle/fire.md B3): the firefighting's (B4) to set each step.</summary>
    public FireWater[] FireFlow = [];
    /// <summary>Her orders before action for fire (docs/battle/fire.md B1): stripped, the doors, the decks wetted. The
    /// damage system keeps it to her side's.</summary>
    public Readiness Readiness;
    /// <summary>Her side's firefighting doctrine (docs/battle/fire.md B4; the damage system keeps it; none: nobody fights
    /// her fires), her fire parties (<see cref="PartyCount"/> of <see cref="MaxParties"/>, made at her first fire), whether
    /// her gun crews have turned to, when each section's fire main was cut (world ms, 0: whole), when she was last hit and
    /// when her parties are next sent.</summary>
    public DamageControlDoctrine DamageControl;
    /// <summary>Her side's flash discipline (docs/battle/magazines.md V5), by its doctrine; unset until her side's orders
    /// are read (<see cref="Magazines.Discipline"/> falls back to the default doctrine's).</summary>
    public FlashDiscipline Flash;
    public const int MaxParties = 16;
    public readonly RepairParty[] Parties = new RepairParty[MaxParties];
    public int PartyCount;
    public bool PartiesMade, TurnedTo;
    public long[] MainCutMs = [];
    public long LastHitMs, NextPlanMs;
    /// <summary>Each magazine's heat (docs/battle/fire.md B2; by room): the dose of its fires so far, the limit it goes up
    /// at (drawn when it first heats, 0 before), how long it's been threatened (s), and whether it's being flooded.</summary>
    public double[] MagazineHeat = [], MagazineLimit = [], MagazineThreatS = [];
    public bool[] MagazineFlood = [];
    /// <summary>A magazine's flood order (docs/battle/magazines.md V6): world ms its valves open (0: none), and the share of
    /// its stow its sprinklers have wetted.</summary>
    public long[] MagazineOrderMs = [];
    public double[] MagazineWet = [];
    /// <summary>The machinery spaces (by room) abandoned to smoke (B2), until their outage runs out.</summary>
    public bool[] Smoked = [];
    /// <summary>The heat release of her fires that show (B2: developed, on the open deck, burning charges), MW.</summary>
    public double FireShowMW;
    public bool[] MountWorks = [], DirectorWorks = [], EyeWorks = [], TubeWorks = [], SearchlightWorks = [];
    /// <summary>Each mount's gun crew and supply at work and each eye's men, 0-1 (docs/battle/crew.md N2,
    /// <see cref="BattleStations"/>): its reload, its hoists and its readings stretched by them.</summary>
    public double[] MountCrew = [], MountSupply = [], EyeCrew = [];
    /// <summary>Which mounts work and the power the plant makes once the outages ending by <see cref="LastingMs"/> are over
    /// (<see cref="Functions.Evaluate"/>): what her captain's fall-out reads (<see cref="Fitness"/>), the lasting state.</summary>
    public bool[] MountLasts = [];
    /// <summary>Each mount sound for the battle whatever its men (its lasting state without them: N3a's posture).</summary>
    public bool[] MountSound = [];
    public double PowerCapLasts = 1;
    public long LastingMs;
    /// <summary>What hits that held left each mount (E4).</summary>
    public MountHarm[] Mount = [];
    /// <summary>Each node's outage while it's <see cref="PartFlags.Down"/>: until when, world ms.</summary>
    public long[] DownUntil = [];
    /// <summary>What put each node down, the longest outage's (<see cref="DownCause"/>): the benches' tally.</summary>
    public DownCause[] DownWhy = [];
    /// <summary>Each director's, eye's and searchlight's outage of its own (E4's shock: a director's gyro, a rangefinder
    /// knocked out of adjustment, the power to a light), not its part's: until when, world ms (0: none).</summary>
    public long[] DirectorDown = [], EyeDown = [], SearchlightDown = [];
    /// <summary>Openings between spaces a burst's gas left (E5: ruptured boundaries, blown doors, sprung seams), m², on
    /// each edge of the cell graph's entry from its lower cell (the Neighbours pool's index): the flooding adds them.</summary>
    public double[] JoinOpenM2 = [];
    /// <summary>Each edge's leaks besides its openings (docs/battle/damage-control.md DC1; damage/17 §7), on the same entry
    /// as <see cref="JoinOpenM2"/>: its sprung seams (m², r(D)), its loosened closures (m²), how far its seams have worked
    /// open (×1 to ×4: 0 until sprung), the working's hazard so far and the threshold it fires at, the highest load it took
    /// (D), and its marks (<see cref="JoinMark"/>).</summary>
    public double[] JoinSeamM2 = [], JoinClosureM2 = [], JoinWork = [], JoinWorkAcc = [], JoinWorkAt = [], JoinD = [];
    public JoinMark[] JoinMarks = [];
    /// <summary>Each edge's flow in the last flooding step (m³/s, on its entry) and when water first stood at a door left
    /// open in it (world ms, 0: never); each cell's inflow from the sea (m³/s): damage control's measures (DC3).</summary>
    public double[] JoinFlow = [], CellInflow = [];
    /// <summary>The portable pumps rigged in each cell, and how many are out of the store (DC4).</summary>
    public byte[] PumpsIn = [];
    public int PumpsRigged;
    /// <summary>DC Central's counterflooding (DC5): the cells flooded on purpose (the pumps leave them), the tank filling
    /// now (-1: none), the tonnes let in so far, and when the valves went to hand (world ms, 0: they haven't).</summary>
    /// <summary>The speed damage control asks for (DC6; knots, infinite: none), since when it has asked for less than she
    /// makes (world ms), and her orders to hold speed whatever it asks (the player's override, the benches').</summary>
    /// <summary>Why her steering went and when (DC7); each node's repair at sea (DC7: 0 not drawn yet, 1 can be fixed, 2
    /// can't), its man-minutes, and the rooms flooded out of action (to come back once pumped down).</summary>
    public SteerCause SteerCause;
    public long SteerSinceMs;
    /// <summary>Damage control's events waiting for the log (DC8: the damage system drains them each step), and which sides'
    /// counterflood tanks it has found full (1 port, 2 starboard).</summary>
    public const int NoteSlots = 8;
    public readonly DcNote[] Notes = new DcNote[NoteSlots];
    public int NoteCount;
    public byte CounterOut;

    /// <summary>A damage-control event for the log (the oldest kept when the ring is full: a step never makes many).</summary>
    public void Note(DcEvent what, int part, int value)
    {
        if (NoteCount < NoteSlots)
            Notes[NoteCount++] = new DcNote(what, part, value);
    }
    public byte[] Fix = [];
    public double[] FixManMin = [];
    public bool[] Drowned = [];
    public double SafeSpeedKn = double.PositiveInfinity;
    public long SafeSinceMs;
    public bool HoldsSpeed;
    public bool[] Counterflooded = [];
    public int CounterTank = -1;
    public double CounterfloodT;
    public long CounterHandMs;
    public long[] JoinWetMs = [];
    /// <summary>Whether her watertight doors left open have been drawn (at her first damage, by her readiness).</summary>
    public bool DoorsDrawn;
    /// <summary>The last cascade's (E5, <see cref="Blast.Cascade"/>): its number, the spaces it reached (BlastRegion, by
    /// BlastStamp) and what each felt, MPa; scratch for the boundaries' marks. E6 reads the pressures.</summary>
    public int BlastId;
    public int[] BlastRegion = [], BlastStamp = [];
    /// <summary>What each reached space felt, MPa, and the gas the region held before it joined, m³ (whether the burst's
    /// hot products got there).</summary>
    public double[] BlastFelt = [], BlastBefore = [];
    internal int[] JoinStamp = [], FaceStamp = [];
    internal byte[] JoinFate = [], FaceFate = [];
    internal double[] JoinFelt = [], FaceFelt = [];
    /// <summary>The class's cells' outside faces (E5): area, plate and middle, worked out at the first burst.</summary>
    internal double[] FaceArea = [], FaceMm = [];
    internal P3[] FacePoint = [];
    internal bool FacesReady;
    /// <summary>When the next timed outage ends, world ms (-1: none does; one for the battle never ends), and the
    /// damage system's event for it: its generation and when it's set for.</summary>
    public long NextClearMs = -1, ClearAtMs = -1;
    public int ClearGen;
    /// <summary>Where its belt leaked from hits that held (E4): the plate loosened there, a ring of the latest
    /// <see cref="LeakMarks"/> (the side plate's row and the point, ship frame).</summary>
    public const int LeakMarks = 32;
    public readonly int[] LeakRow = new int[LeakMarks];
    public readonly P3[] LeakAt = new P3[LeakMarks];
    public int Leaks;

    /// <summary>The crew (E6 of docs/battle/shells-armour-damage.md): the men out of action now (the killed, the wounded
    /// out for the action, the walking wounded not back yet; and the men the torpedo's rule takes, which aren't split
    /// yet), and the killed and wounded so far (the fire's too: docs/battle/fire.md B2).</summary>
    public double CrewLost, Killed, Wounded;
    /// <summary>At each station, the men killed or out there now: a part's (by component), then a cell's
    /// (<see cref="Casualties"/>'s groups); and those away from it in a party (docs/battle/crew.md N1).</summary>
    public int[] Gone = [], Away = [];
    /// <summary>Where the men stand by the captain's order (docs/battle/crew.md N3a, <see cref="Reliefs"/>): the men
    /// arrived at each station from elsewhere (reliefs), each mount closed up or under cover, its men under cover and the
    /// shelter they wait in (a cell's group, -1 none), and what each fire party has lent and to which station.</summary>
    public int[] In = [];
    public bool[] ClosedUp = [];
    public int[] Covered = [], MountShelter = [];
    public readonly int[] PartyLent = new int[MaxParties], PartyLentTo = new int[MaxParties];
    /// <summary>The men's nerve (docs/battle/crew.md N3b, <see cref="Steadiness"/>): their experience (her side's, 0-3); the
    /// stations that have duress (by group: its index, -1 none; by station: its group, whether it's open, how its men are
    /// covered from a burst near it); and by station: its duress's channels (<see cref="Steadiness.Channels"/> a station, the
    /// last its sensitising) as of when they were last faded, the men gone on their own (shaken) and those present and not
    /// working (frozen), since when it's been calm (0: it isn't), its officer (0 there, -1 hit and none coming, else when a
    /// relief officer gets there), the nerve it's lost for good, whether it's over its nerve, when it last burned, and
    /// this hit's marks; the open stations by x; the stations to look at again; the spare officers; when they're next due.</summary>
    public double Experience = Core.Experience.Trained;
    public int[] StationOf = [], StationGroup = [];
    public bool[] StationOpen = [];
    public byte[] StationCover = [];
    public double[] Duress = [];
    public long[] DuressMs = [], CalmMs = [], OfficerAt = [], BurnMs = [];
    public int[] Shaken = [], Frozen = [];
    public double[] NerveKept = [];
    public bool[] Over = [];
    internal int[] InsideStamp = [], NearStamp = [];
    internal int[] OpenByX = [];
    internal int[] Active = [];
    internal bool[] IsActive = [];
    internal int ActiveN, StirredId;
    /// <summary>Whether the hit (the casualty pass) under way added to a station's duress (N3b).</summary>
    public bool Stirred => StirredId == RiskId;
    public int SpareOfficers;
    public long NerveDueMs;

    internal void NerveDue(long at)
    {
        NerveDueMs = at;
        Due(at);
    }
    /// <summary>The men on their way (N3a): when they get there, to which station (-1 - g: back to their own g), how
    /// many; a pool of <see cref="MoveSlots"/>.</summary>
    public const int MoveSlots = 64;
    public readonly long[] MoveMs = new long[MoveSlots];
    public readonly int[] MoveTo = new int[MoveSlots], MoveMen = new int[MoveSlots];
    public int Moves;

    /// <summary>Men on their way to a station (or back to their own: -1 - g), there then; with the pool full the soonest
    /// due arrive now to make room.</summary>
    internal void Move(int to, int men, long at)
    {
        if (men <= 0)
            return;
        if (Moves == MoveSlots)
        {
            int soonest = 0;
            for (int i = 1; i < Moves; i++)
                if (MoveMs[i] < MoveMs[soonest])
                    soonest = i;
            Arrive(soonest);
        }
        (MoveMs[Moves], MoveTo[Moves], MoveMen[Moves]) = (at, to, men);
        Moves++;
        Due(at);
    }

    /// <summary>A move's men there, its slot taken by the last.</summary>
    void Arrive(int i)
    {
        int to = MoveTo[i], men = MoveMen[i];
        if (to >= 0)
            In[to] += men;
        else
            Away[-1 - to] = Math.Max(Away[-1 - to] - men, 0);
        Moves--;
        (MoveMs[i], MoveTo[i], MoveMen[i]) = (MoveMs[Moves], MoveTo[Moves], MoveMen[Moves]);
    }
    /// <summary>The walking wounded on their way back (E6): when, to which station, how many; a pool of
    /// <see cref="ReturnSlots"/>.</summary>
    public const int ReturnSlots = 64;
    public readonly long[] ReturnMs = new long[ReturnSlots];
    public readonly int[] ReturnGroup = new int[ReturnSlots], ReturnMen = new int[ReturnSlots];
    public int Returns;
    /// <summary>Scratch for one hit's risks on its groups of men (<see cref="Casualties"/>): the log of the share left
    /// unkilled and unhurt by each risk so far, the hit's stamp, the groups it touched.</summary>
    internal double[] RiskK = [], RiskC = [];
    internal int[] RiskStamp = [], Touched = [];
    internal int RiskId, TouchedN;
    /// <summary>When the casualty pass under way happens (world ms): where the parties stand then.</summary>
    internal long RiskMs;
    public ShipStatus Status;
    /// <summary>How it went down (None while afloat), when it began to and when it's gone under (ms).</summary>
    public LossKind Loss;
    public long LossMs, GoneMs;
    public Attitude Attitude;

    /// <summary>Its attitude at a moment (ms): the one it settled to, or going down, how far under it is then
    /// (<see cref="Attitude.At"/>).</summary>
    public Attitude AttitudeAt(long ms) => Attitude.At(Class, Attitude, Loss, LossMs, GoneMs, ms);
    /// <summary>The water aboard, tonnes, and the transverse GM with it in, m (the class's while dry).</summary>
    public double WaterT, GmM;
    /// <summary>The share of its power the plant can still make (boilers and shafts), 0-1.</summary>
    public double PowerCap = 1;
    public Steering Steering;
    /// <summary>The rudder's angle when it jammed, radians.</summary>
    public double JamRudderRad;
    /// <summary>Whether it's conned (docs/battle/crew.md N2: a bridge or the conning tower standing with her command party
    /// there, else aft control once the control has shifted), where from (0 forward, 1 aft control, -1 nowhere) and when
    /// the control shifting aft gets there (world ms; 0: none shifting). What it costs beyond counterflooding by hand
    /// comes with the signals (5.1).</summary>
    public bool Conned = true;
    public int ConnAt;
    public long ConnShiftMs;

    /// <summary>Each boiler room's stokehold at work (docs/battle/crew.md N2: its steam's share by its men, by room), and
    /// until when a drop is held on what's already in the fires (world ms; 0: none held).</summary>
    public double[] Stoke = [];
    public long[] StokeUntilMs = [];
    /// <summary>Each part's and each cell's fire space (-1: none), the class's, for a station's space (N2).</summary>
    public int[] PartSpace = [], CellSpace = [];

    /// <summary>A boiler room's fires burning on until then (on the outage clock).</summary>
    internal void HoldStoke(int room, long until)
    {
        StokeUntilMs[room] = until;
        Due(until);
    }

    /// <summary>The control shifting aft, there then (on the outage clock: the clear event re-works her functions).</summary>
    internal void ShiftConn(long until)
    {
        ConnShiftMs = until;
        Due(until);
    }
    /// <summary>Hits taken, and whether it's been touched at all (a ship never hit hashes as before); who hit it last (an
    /// entity and battery, for the log's line when it goes down).</summary>
    public int Hits, LastShooter, LastBattery;
    public bool Damaged;
    /// <summary>Bumped each time what it can do may have changed (the gunnery system rebuilds a battery's instruments).</summary>
    public int Version;

    /// <summary>The flooding and fire's clock (the damage system's): when it last stepped, when it steps next (-1: it
    /// doesn't, dry and not burning), the step's generation (a step brought sooner cancels the one it replaces), and the
    /// share of its power it was last held to with its water's weight in (the ship store's).</summary>
    public long StepMs, NextStepMs = -1;
    public int StepGen;
    public double HeldCap = 1;
    /// <summary>The flooding's own clock within it (docs/battle/coarse.md J6): when the water last stepped, when it's due
    /// next (every FloodMs while it moves, FloodIdleMs once settled, whether or not she burns), and the hoses' water put in
    /// since, m³.</summary>
    public long FloodStepMs, FloodDueMs, FloodEveryMs = 1000;
    public double FireWaterM3;

    internal void Reset(ShipClass c)
    {
        if (Class != c)
        {
            Class = c;
            State = new PartState[c.NodeCount];
            Flags = new PartFlags[c.NodeCount];
            WaterM3 = new double[c.Cells.Length];
            BreachM2 = new double[c.Cells.Length];
            BreachY = new double[c.Cells.Length];
            BreachZ = new double[c.Cells.Length];
            (HoleOpenM2, HoleZ, HoleDoneM2) = (new double[c.Cells.Length * HoleClasses], new double[c.Cells.Length * HoleClasses], new double[c.Cells.Length * HoleClasses]);
            (HoleLeft, HoleOpenN) = (new double[c.Cells.Length * HoleClasses], new int[c.Cells.Length * HoleClasses]);
            CellFire = new bool[c.Cells.Length];
            CellOpened = new bool[c.Cells.Length];
            Fire = new BurnState[c.FireSpaces.Length];
            FireFlow = new FireWater[c.FireSpaces.Length];
            MainCutMs = new long[c.Sections.Length];
            (MagazineHeat, MagazineLimit, MagazineThreatS, MagazineFlood) =
                (new double[c.Rooms.Length], new double[c.Rooms.Length], new double[c.Rooms.Length], new bool[c.Rooms.Length]);
            (MagazineOrderMs, MagazineWet) = (new long[c.Rooms.Length], new double[c.Rooms.Length]);
            Smoked = new bool[c.Rooms.Length];
            (Stoke, StokeUntilMs) = (new double[c.Rooms.Length], new long[c.Rooms.Length]);
            (PartSpace, CellSpace) = (new int[c.Components.Length], new int[c.Cells.Length]);
            Array.Fill(PartSpace, -1);
            Array.Fill(CellSpace, -1);
            for (int i = 0; i < c.FireSpaces.Length; i++)
            {
                ref readonly var row = ref c.FireSpaces[i];
                if (row.Kind == FireSpaceKind.Part)
                    PartSpace[row.Index] = i;
                else if (row.Kind == FireSpaceKind.Cell)
                    CellSpace[row.Index] = i;
            }
            (Level, LevelsFuel) = (new double[c.Rooms.Length], double.NaN);
            MountWorks = new bool[c.Mounts.Length];
            (MountCrew, MountSupply, EyeCrew) = (new double[c.Mounts.Length], new double[c.Mounts.Length], new double[c.Eyes.Length]);
            MountLasts = new bool[c.Mounts.Length];
            MountSound = new bool[c.Mounts.Length];
            DirectorWorks = new bool[c.Directors.Length];
            EyeWorks = new bool[c.Eyes.Length];
            TubeWorks = new bool[c.Tubes.Length];
            SearchlightWorks = new bool[c.Searchlights.Length];
            Mount = new MountHarm[c.Mounts.Length];
            DownUntil = new long[c.NodeCount];
            DownWhy = new DownCause[c.NodeCount];
            DirectorDown = new long[c.Directors.Length];
            EyeDown = new long[c.Eyes.Length];
            SearchlightDown = new long[c.Searchlights.Length];
            JoinOpenM2 = new double[c.NeighbourCount];
            (JoinSeamM2, JoinClosureM2, JoinWork) = (new double[c.NeighbourCount], new double[c.NeighbourCount], new double[c.NeighbourCount]);
            (JoinWorkAcc, JoinWorkAt, JoinD) = (new double[c.NeighbourCount], new double[c.NeighbourCount], new double[c.NeighbourCount]);
            JoinMarks = new JoinMark[c.NeighbourCount];
            (JoinFlow, JoinWetMs, CellInflow) = (new double[c.NeighbourCount], new long[c.NeighbourCount], new double[c.Cells.Length]);
            PumpsIn = new byte[c.Cells.Length];
            Counterflooded = new bool[c.Cells.Length];
            (Fix, FixManMin, Drowned) = (new byte[c.NodeCount], new double[c.NodeCount], new bool[c.NodeCount]);
            (BlastRegion, BlastStamp, BlastFelt) = (new int[c.Cells.Length], new int[c.Cells.Length], new double[c.Cells.Length]);
            BlastBefore = new double[c.Cells.Length];
            (JoinStamp, JoinFate, JoinFelt) = (new int[c.NeighbourCount], new byte[c.NeighbourCount], new double[c.NeighbourCount]);
            (FaceStamp, FaceFate, FaceFelt) = (new int[4 * c.Cells.Length], new byte[4 * c.Cells.Length], new double[4 * c.Cells.Length]);
            (FaceArea, FaceMm, FacePoint, FacesReady) = (new double[4 * c.Cells.Length], new double[4 * c.Cells.Length], new P3[4 * c.Cells.Length], false);
            int groups = c.Components.Length + c.Cells.Length;
            (Gone, Away, RiskK, RiskC, RiskStamp, Touched) = (new int[groups], new int[groups], new double[groups], new double[groups], new int[groups], new int[groups]);
            In = new int[groups];
            (ClosedUp, Covered, MountShelter) = (new bool[c.Mounts.Length], new int[c.Mounts.Length], new int[c.Mounts.Length]);
            for (int m = 0; m < c.Mounts.Length; m++)
                MountShelter[m] = Reliefs.ShelterOf(c, m);
            Steadiness.Setup(c, this);
        }
        else
        {
            Array.Clear(Gone);
            Array.Clear(Away);
            Array.Clear(In);
            Array.Clear(Covered);
            Array.Clear(JoinOpenM2);
            Array.Clear(JoinSeamM2);
            Array.Clear(JoinClosureM2);
            Array.Clear(JoinWork);
            Array.Clear(JoinWorkAcc);
            Array.Clear(JoinWorkAt);
            Array.Clear(JoinD);
            Array.Clear(JoinMarks);
            Array.Clear(JoinFlow);
            Array.Clear(JoinWetMs);
            Array.Clear(CellInflow);
            Array.Clear(PumpsIn);
            Array.Clear(Counterflooded);
            Array.Clear(Fix);
            Array.Clear(FixManMin);
            Array.Clear(Drowned);
            Array.Clear(BlastStamp);
            Array.Clear(JoinStamp);
            Array.Clear(FaceStamp);
            Array.Clear(Mount);
            Array.Clear(DownUntil);
            Array.Clear(DownWhy);
            Array.Clear(DirectorDown);
            Array.Clear(EyeDown);
            Array.Clear(SearchlightDown);
            Array.Clear(State);
            Array.Clear(Flags);
            Array.Clear(WaterM3);
            Array.Clear(BreachM2);
            Array.Clear(BreachY);
            Array.Clear(BreachZ);
            Array.Clear(HoleOpenM2);
            Array.Clear(HoleZ);
            Array.Clear(HoleDoneM2);
            Array.Clear(HoleOpenN);
            Array.Clear(CellFire);
            Array.Clear(CellOpened);
            Array.Clear(Fire);
            Array.Clear(FireFlow);
            Array.Clear(MainCutMs);
            Array.Clear(MagazineHeat);
            Array.Clear(MagazineLimit);
            Array.Clear(MagazineThreatS);
            Array.Clear(MagazineFlood);
            Array.Clear(MagazineOrderMs);
            Array.Clear(MagazineWet);
            Array.Clear(Smoked);
        }
        Array.Fill(HoleLeft, 1.0);
        Array.Fill(MountWorks, true);
        Array.Fill(MountSound, true);
        Array.Fill(MountCrew, 1.0);
        Array.Fill(MountSupply, 1.0);
        Array.Fill(EyeCrew, 1.0);
        Array.Fill(MountLasts, true);
        (PowerCapLasts, LastingMs) = (1, 0);
        Array.Fill(DirectorWorks, true);
        Array.Fill(EyeWorks, true);
        Array.Fill(TubeWorks, true);
        Array.Fill(SearchlightWorks, true);
        (CrewLost, Killed, Wounded, Returns) = (0, 0, 0, 0);
        (Status, Loss, Attitude, PowerCap, Steering, JamRudderRad, Conned, Hits, Damaged) = (ShipStatus.Afloat, LossKind.None, Attitude.Upright, 1, Steering.Free, 0, true, 0, false);
        (ConnAt, ConnShiftMs) = (0, 0);
        Array.Fill(ClosedUp, true);
        Moves = 0;
        Steadiness.Reset(this);
        Array.Clear(PartyLent);
        Array.Clear(PartyLentTo);
        Array.Fill(Stoke, 1.0);
        Array.Clear(StokeUntilMs);
        (WaterT, GmM, StepMs, NextStepMs, HeldCap, LastShooter, LastBattery, LossMs, GoneMs) = (0, c.Hydrostatics.GmT, 0, -1, 1, 0, 0, 0, 0);
        (FloodStepMs, FloodDueMs, FloodEveryMs, FireWaterM3) = (0, 0, 1000, 0);
        (NextClearMs, ClearAtMs, Leaks, DoorsDrawn, PumpsRigged) = (-1, -1, 0, false, 0);
        (CounterTank, CounterfloodT, CounterHandMs) = (-1, 0, 0);
        (SafeSpeedKn, SafeSinceMs, HoldsSpeed, SteerCause, SteerSinceMs) = (double.PositiveInfinity, 0, false, SteerCause.None, 0);
        (NoteCount, CounterOut) = (0, 0);
        (PartyCount, PartiesMade, TurnedTo, LastHitMs, NextPlanMs, FireShowMW) = (0, false, false, 0, 0, 0);
        ClearGen++;
        Version++;
    }

    /// <summary>A part made worse (never better): the worse of its state and the new one.</summary>
    public bool Worsen(int node, PartState s)
    {
        if (s <= State[node])
            return false;
        State[node] = s;
        return true;
    }

    /// <summary>Holes to the sea in a cell, at a point (ship frame): count of them making areaM2 together (or a seam's
    /// leak, sprung plating rather than holes: caulked, not plugged). Its area adds, its height and side are the holes'
    /// mean by area; each is kept by its class (docs/battle/damage-control.md DC2), by the size of one hole.</summary>
    public void Hole(int cell, double areaM2, P3 at, int count = 1, bool seam = false)
    {
        double a0 = BreachM2[cell], a1 = a0 + areaM2;
        if (a1 <= 0 || !(areaM2 > 0))
            return;
        BreachY[cell] = (BreachY[cell] * a0 + at.Y * areaM2) / a1;
        BreachZ[cell] = (BreachZ[cell] * a0 + at.Z * areaM2) / a1;
        BreachM2[cell] = a1;
        int h = cell * HoleClasses + (int)(seam ? HoleClass.Seam : Classify(areaM2 / Math.Max(count, 1)));
        double o0 = HoleOpenM2[h], d0 = HoleDoneM2[h];
        HoleZ[h] = (HoleZ[h] * (o0 + d0) + at.Z * areaM2) / (o0 + d0 + areaM2);   // the class's holes, worked on or not
        HoleOpenM2[h] = o0 + areaM2;
        HoleOpenN[h] += Math.Max(count, 1);
    }

    /// <summary>What a cell's holes of a class (an index into <see cref="HoleOpenM2"/>) let through: open + worked × left.</summary>
    public double HoleM2(int h) => HoleOpenM2[h] + HoleDoneM2[h] * HoleLeft[h];

    /// <summary>Whether any of a cell's holes is under a height (the sea outside it).</summary>
    public bool HoledUnder(int cell, double z)
    {
        for (int h = cell * HoleClasses; h < (cell + 1) * HoleClasses; h++)
            if (HoleM2(h) > 0 && HoleZ[h] < z)
                return true;
        return false;
    }

    /// <summary>A cell's breach counted again from its holes by class (open + worked × left).</summary>
    public void Recount(int cell)
    {
        double sum = 0;
        for (int i = cell * HoleClasses; i < (cell + 1) * HoleClasses; i++)
            sum += HoleOpenM2[i] + HoleDoneM2[i] * HoleLeft[i];
        BreachM2[cell] = sum;
    }

    /// <summary>A cell's open holes of a class worked on by damage control (DC3): they leak left of their area from now
    /// (mixed by area with any worked on before), and the cell's breach is counted again.</summary>
    public void Worked(int cell, int h, double left)
    {
        double open = HoleOpenM2[h], done = HoleDoneM2[h];
        if (open <= 0)
            return;
        HoleLeft[h] = (done * HoleLeft[h] + open * left) / (done + open);
        (HoleDoneM2[h], HoleOpenM2[h], HoleOpenN[h]) = (done + open, 0, 0);
        Recount(cell);
    }

    /// <summary>A hole's class by its own area (09 §4): plugged up to 150 cm², patched to 0.3 m², a collision mat's to
    /// 5 m², beyond that the dockyard's.</summary>
    public static HoleClass Classify(double areaM2) => areaM2 <= PlugM2 ? HoleClass.Plug : areaM2 <= PatchM2 ? HoleClass.Patch :
        areaM2 <= MatM2 ? HoleClass.Mat : HoleClass.Beyond;

    public const double PlugM2 = 0.015, PatchM2 = 0.3, MatM2 = 5;

    /// <summary>A part damaged once more: ok to damaged, damaged to disabled.</summary>
    public bool Harm(int node) => Worsen(node, State[node] == PartState.Ok ? PartState.Damaged : PartState.Disabled);

    /// <summary>How much a node still gives: 1 working, 0.5 damaged, 0 out (or down for a while).</summary>
    public double Health(int node) => (Flags[node] & PartFlags.Down) != 0 ? 0 : State[node] switch
    {
        PartState.Ok => 1,
        PartState.Damaged => 0.5,
        _ => 0,
    };

    /// <summary>How much a node gives once its outage, if it ends by then (world ms), is over: <see cref="Health"/> but
    /// for that.</summary>
    public double HealthBy(int node, long ms) => (Flags[node] & PartFlags.Down) != 0 && DownUntil[node] <= ms ? State[node] switch
    {
        PartState.Ok => 1,
        PartState.Damaged => 0.5,
        _ => 0,
    } : Health(node);

    public bool Has(int node, PartFlags f) => (Flags[node] & f) != 0;

    /// <summary>A node out for a while, from now for ms (E4): down until then, or later if it already was; the longer
    /// outage's cause kept.</summary>
    public void Down(int node, long now, long ms, DownCause why)
    {
        if (ms <= 0)
            return;
        long until = Until(now, ms);
        Flags[node] |= PartFlags.Down;
        if (until > DownUntil[node])
            (DownUntil[node], DownWhy[node]) = (until, why);
        Due(until);
    }

    /// <summary>A director or an eye out of its own for a while, from now for ms (E4's shock): until then, or later if it
    /// already was.</summary>
    public void Out(long[] downs, int i, long now, long ms)
    {
        if (ms <= 0)
            return;
        long until = Until(now, ms);
        downs[i] = Math.Max(downs[i], until);
        Due(until);
    }

    /// <summary>What a hit that held did to a mount's turret, from now (E4): jammed (the later of its jams lasting, with
    /// its cause), stiff, a gun more out (no more than it has), its sights gone; its crew stunned with it.</summary>
    public void Strike(int mount, in TurretHarm h, long now)
    {
        ref var a = ref Mount[mount];
        ref readonly var row = ref Class.Mounts[mount];
        long until = Until(now, h.Ms);
        switch (h.Blow)
        {
            case TurretBlow.Jam:
                Flags[row.Component] |= PartFlags.Jammed;
                if (until > a.JamUntil)
                    (a.JamUntil, a.Jam) = (until, h.Cause);
                Due(until);
                break;
            case TurretBlow.Stiff:
                a.StiffUntil = Math.Max(a.StiffUntil, until);
                Due(until);
                break;
            case TurretBlow.GunOut:
                a.GunsOut = Math.Min(a.GunsOut + 1, Class.TurretTypes[row.Type].Barrels);
                a.GunUntil = Math.Max(a.GunUntil, until);
                Due(until);
                break;
            case TurretBlow.Sights:
                a.Sights = true;
                break;
        }
        Down(row.Component, now, h.StunMs, DownCause.Stunned);
    }

    /// <summary>A leak in a belt's plate at a point (E4): the plate loosened there.</summary>
    public void Leaked(int row, P3 at)
    {
        int i = Leaks++ % LeakMarks;
        (LeakRow[i], LeakAt[i]) = (row, at);
    }

    /// <summary>Whether a belt's plate leaked within m of a point before: loosened there (E4).</summary>
    public bool Loosened(int row, P3 at, double m)
    {
        for (int i = 0, n = Math.Min(Leaks, LeakMarks); i < n; i++)
        {
            ref readonly var p = ref LeakAt[i];
            if (LeakRow[i] == row && (p.X - at.X) * (p.X - at.X) + (p.Y - at.Y) * (p.Y - at.Y) + (p.Z - at.Z) * (p.Z - at.Z) <= m * m)
                return true;
        }
        return false;
    }

    /// <summary>Whether a mount trains stiffly now (E4).</summary>
    public bool Stiff(int mount, long now) => Mount[mount].StiffUntil > now;

    /// <summary>The guns a mount still fires of its barrels (E4: its guns out don't).</summary>
    public int Guns(int mount) => Math.Max(Class.TurretTypes[Class.Mounts[mount].Type].Barrels - Mount[mount].GunsOut, 0);

    /// <summary>Whether a mount works once the outages ending by then (world ms) are over: its function's worth then
    /// (<paramref name="value"/>), its jam cleared and its guns back if they're due by then.</summary>
    public bool WorksBy(int mount, double value, long ms)
    {
        ref readonly var a = ref Mount[mount];
        bool jammed = Has(Class.Mounts[mount].Component, PartFlags.Jammed) && !(a.JamUntil > 0 && a.JamUntil <= ms);
        bool guns = a.GunUntil > 0 && a.GunUntil <= ms || Guns(mount) > 0;
        return value > 0 && !jammed && guns;
    }

    /// <summary>Whether a mount can lay its guns: it works, and at local control, with its sights (E4: a mount with its
    /// sights out fires only by a director).</summary>
    public bool Lays(int mount, bool local) => MountWorks[mount] && !(local && Mount[mount].Sights);

    /// <summary>The timed outages that have run out by now ended (E4): nodes no longer down, jams cleared, stiffness and
    /// guns out over; when the next ends. Whether anything ended.</summary>
    public bool Clear(long now)
    {
        bool any = false;
        long next = long.MaxValue;
        for (int i = 0; i < DownUntil.Length; i++)
        {
            if ((Flags[i] & PartFlags.Down) == 0)
                continue;
            if (DownUntil[i] <= now)
                (Flags[i], DownUntil[i], any) = (Flags[i] & ~PartFlags.Down, 0, true);
            else
                next = Math.Min(next, DownUntil[i]);
        }
        any |= Clear(DirectorDown, now, ref next) | Clear(EyeDown, now, ref next) | Clear(SearchlightDown, now, ref next);
        var mounts = Class.Mounts;
        for (int m = 0; m < Mount.Length; m++)
        {
            ref var a = ref Mount[m];
            if (a.JamUntil > 0 && a.JamUntil <= now)
            {
                Flags[mounts[m].Component] &= ~PartFlags.Jammed;
                (a.JamUntil, a.Jam, any) = (0, JamCause.None, true);
            }
            if (a.StiffUntil > 0 && a.StiffUntil <= now)
                (a.StiffUntil, any) = (0, true);
            if (a.GunUntil > 0 && a.GunUntil <= now)
                (a.GunUntil, a.GunsOut, any) = (0, 0, true);
            next = Math.Min(next, Later(a.JamUntil, now));
            next = Math.Min(next, Later(a.StiffUntil, now));
            next = Math.Min(next, Later(a.GunUntil, now));
        }
        // a stokehold's fires burned down to what its men keep up (N2)
        for (int r = 0; r < StokeUntilMs.Length; r++)
            if (StokeUntilMs[r] > 0)
            {
                if (StokeUntilMs[r] <= now)
                    any = true;
                else
                    next = Math.Min(next, StokeUntilMs[r]);
            }
        // the control shifted aft (N2): her functions to work out again
        if (ConnShiftMs > 0)
        {
            if (ConnShiftMs <= now)
                any = true;
            else
                next = Math.Min(next, ConnShiftMs);
        }
        // the men's nerve to look at again, a relief officer come (N3b): worked out with her functions
        if (NerveDueMs > 0)
        {
            if (NerveDueMs <= now)
                any = true;
            else
                next = Math.Min(next, NerveDueMs);
        }
        // the men on their way there (N3a)
        for (int i = Moves - 1; i >= 0; i--)
        {
            if (MoveMs[i] <= now)
            {
                Arrive(i);
                any = true;
            }
            else
                next = Math.Min(next, MoveMs[i]);
        }
        // the walking wounded back at their stations, which work by their men (docs/battle/crew.md N2)
        for (int i = Returns - 1; i >= 0; i--)
        {
            if (ReturnMs[i] <= now)
            {
                Return(i);
                any = true;
            }
            else
                next = Math.Min(next, ReturnMs[i]);
        }
        NextClearMs = next is long.MaxValue ? -1 : next;
        return any;
    }

    /// <summary>Walking wounded leaving a station, back at it then (E6): one more on the way back; with the pool full the
    /// soonest due come back now to make room.</summary>
    public void Back(int group, int men, long at)
    {
        if (men <= 0)
            return;
        if (Returns == ReturnSlots)
        {
            int soonest = 0;
            for (int i = 1; i < Returns; i++)
                if (ReturnMs[i] < ReturnMs[soonest])
                    soonest = i;
            Return(soonest);
        }
        (ReturnMs[Returns], ReturnGroup[Returns], ReturnMen[Returns]) = (at, group, men);
        Returns++;
        Due(at);
    }

    /// <summary>A return's men back at their station (or their party: a group past the stations,
    /// <see cref="Casualties.PartyGroup"/>), its slot taken by the last.</summary>
    void Return(int i)
    {
        int g = ReturnGroup[i], men;
        if (g < Gone.Length)
        {
            men = Math.Min(ReturnMen[i], Gone[g]);
            Gone[g] -= men;
        }
        else if (g < Gone.Length + MaxParties)
        {
            men = ReturnMen[i];
            Parties[g - Gone.Length].Men += men;
        }
        else
        {
            men = ReturnMen[i];
            Covered[g - Gone.Length - MaxParties] += men;
        }
        CrewLost = Math.Max(CrewLost - men, 0);
        Returns--;
        (ReturnMs[i], ReturnGroup[i], ReturnMen[i]) = (ReturnMs[Returns], ReturnGroup[Returns], ReturnMen[Returns]);
    }

    /// <summary>A row of outages ended by now (zeroed), the next of the rest's end; whether any ended.</summary>
    static bool Clear(long[] downs, long now, ref long next)
    {
        bool any = false;
        for (int i = 0; i < downs.Length; i++)
        {
            if (downs[i] == 0)
                continue;
            if (downs[i] <= now)
                (downs[i], any) = (0, true);
            else
                next = Math.Min(next, Later(downs[i], now));
        }
        return any;
    }

    /// <summary>An outage's end if it's still to come and ends at all, else never (long.MaxValue).</summary>
    static long Later(long until, long now) => until > now ? until : long.MaxValue;

    /// <summary>An outage's end, from now for ms (<see cref="NonPen.Battle"/> stays).</summary>
    static long Until(long now, long ms) => ms == NonPen.Battle ? NonPen.Battle : now + ms;

    /// <summary>An outage ending then: the next to end, if it's sooner (one for the battle never ends).</summary>
    void Due(long until)
    {
        if (until != NonPen.Battle && until > 0 && (NextClearMs < 0 || until < NextClearMs))
            NextClearMs = until;
    }

    /// <summary>Whether a launcher (the class's Launchers) can fire: its mount or its tube works.</summary>
    public bool LauncherWorks(int launcher)
    {
        ref readonly var l = ref Class.Launchers[launcher];
        return l.Kind == LauncherKind.Mount ? MountWorks[l.Index] : TubeWorks[l.Index];
    }

    /// <summary>The working mounts of n (for the strip and tests).</summary>
    public int MountsWorking
    {
        get
        {
            int n = 0;
            foreach (bool w in MountWorks)
                if (w)
                    n++;
            return n;
        }
    }

    /// <summary>The fires burning: the fire spaces alight.</summary>
    public int Fires
    {
        get
        {
            int n = 0;
            foreach (ref readonly var f in Fire.AsSpan())
                if (f.Burning)
                    n++;
            return n;
        }
    }

    public double BreachTotalM2
    {
        get
        {
            double a = 0;
            foreach (double b in BreachM2)
                a += b;
            return a;
        }
    }

    public void Hash(ref StateHash h)
    {
        h.Add(Hits);
        h.Add(CrewLost);
        if (Killed + Wounded > 0)
        {
            h.Add(Killed);
            h.Add(Wounded);
        }
        h.Add((int)Status | (int)Loss << 8);
        if (ConnAt != 0 || ConnShiftMs != 0)
        {
            h.Add(ConnAt + 7000);
            h.Add(ConnShiftMs);
        }
        for (int r = 0; r < Stoke.Length; r++)
            if (Stoke[r] != 1 || StokeUntilMs[r] != 0)
            {
                h.Add(r + 8000);
                h.Add(Stoke[r]);
                h.Add(StokeUntilMs[r]);
            }
        h.Add(PowerCap);
        h.Add(WaterT);
        if (FloodDueMs != 0)
        {
            h.Add(FloodStepMs);
            h.Add(FloodDueMs);
            h.Add(FloodEveryMs);
            h.Add(FireWaterM3);
        }
        h.Add((int)Steering);
        h.Add(JamRudderRad);
        for (int i = 0; i < State.Length; i++)
            if (State[i] != PartState.Ok || Flags[i] != PartFlags.None)
            {
                h.Add(i);
                h.Add((int)State[i] | (int)Flags[i] << 8);
                if ((Flags[i] & PartFlags.Down) != 0)
                    h.Add(DownUntil[i]);
            }
        if (Leaks > 0)
            h.Add(Leaks);
        for (int i = 0; i < JoinOpenM2.Length; i++)
            if (JoinOpenM2[i] != 0)
            {
                h.Add(i + 2000);
                h.Add(JoinOpenM2[i]);
            }
        if (SteerCause != SteerCause.None)
            h.Add((int)SteerCause);
        for (int i = 0; i < Fix.Length; i++)
            if (Fix[i] != 0 || Drowned[i])
            {
                h.Add(i + 6000);
                h.Add(Fix[i] | (Drowned[i] ? 4 : 0));
            }
        if (double.IsFinite(SafeSpeedKn))
        {
            h.Add(SafeSpeedKn);
            h.Add(SafeSinceMs);
        }
        if (CounterfloodT > 0)
        {
            h.Add(CounterfloodT);
            h.Add(CounterTank);
        }
        for (int i = 0; i < PumpsIn.Length; i++)
            if (PumpsIn[i] != 0)
            {
                h.Add(i + 5000);
                h.Add(PumpsIn[i]);
            }
        for (int i = 0; i < HoleDoneM2.Length; i++)
            if (HoleDoneM2[i] != 0)
            {
                h.Add(i + 4000);
                h.Add(HoleDoneM2[i]);
                h.Add(HoleLeft[i]);
            }
        for (int i = 0; i < JoinSeamM2.Length; i++)
            if (JoinSeamM2[i] != 0 || JoinClosureM2[i] != 0 || JoinMarks[i] != JoinMark.None)
            {
                h.Add(i + 3000);
                h.Add(JoinSeamM2[i]);
                h.Add(JoinClosureM2[i]);
                h.Add(JoinWork[i]);
                h.Add(JoinWorkAcc[i]);
                h.Add((int)JoinMarks[i]);
            }
        for (int i = 0; i < DirectorDown.Length; i++)
            if (DirectorDown[i] != 0)
            {
                h.Add(i);
                h.Add(DirectorDown[i]);
            }
        for (int i = 0; i < EyeDown.Length; i++)
            if (EyeDown[i] != 0)
            {
                h.Add(i + 1000);
                h.Add(EyeDown[i]);
            }
        for (int i = 0; i < SearchlightDown.Length; i++)
            if (SearchlightDown[i] != 0)
            {
                h.Add(i + 6000);
                h.Add(SearchlightDown[i]);
            }
        for (int m = 0; m < Mount.Length; m++)
        {
            ref readonly var a = ref Mount[m];
            if (a.JamUntil == 0 && a.StiffUntil == 0 && a.GunsOut == 0 && !a.Sights)
                continue;
            h.Add(m);
            h.Add((int)a.Jam | a.GunsOut << 8 | (a.Sights ? 1 << 16 : 0));
            h.Add(a.JamUntil);
            h.Add(a.StiffUntil);
            h.Add(a.GunUntil);
        }
        for (int p = 0; p < PartyCount; p++)
        {
            ref readonly var party = ref Parties[p];
            h.Add(p + 4000);
            h.Add(party.Men);
            h.Add(party.Target);
            h.Add(party.ArriveMs);
            h.Add(party.StopMs);
            h.Add(party.Home);
            h.Add(party.Place);
        }
        for (int g = 0; g < Away.Length; g++)
            if (Away[g] != 0 || In[g] != 0)
            {
                h.Add(g + 6000);
                h.Add(Away[g]);
                h.Add(In[g]);
            }
        for (int m = 0; m < ClosedUp.Length; m++)
            if (!ClosedUp[m] || Covered[m] != 0)
            {
                h.Add(m + 9000);
                h.Add(Covered[m] * 2 + (ClosedUp[m] ? 1 : 0));
            }
        for (int i = 0; i < Moves; i++)
        {
            h.Add(MoveMs[i]);
            h.Add(MoveTo[i] * 10_000 + MoveMen[i]);
        }
        for (int n = 0; n < Shaken.Length; n++)
            if (Shaken[n] != 0 || Frozen[n] != 0 || OfficerAt[n] != 0)
            {
                h.Add(n + 9700);
                h.Add(Shaken[n] * 10_000 + Frozen[n]);
                h.Add(OfficerAt[n]);
            }
        for (int p = 0; p < PartyCount; p++)
            if (PartyLent[p] != 0)
            {
                h.Add(p + 9500);
                h.Add(PartyLent[p] * 10_000 + PartyLentTo[p]);
            }
        for (int i = 0; i < MainCutMs.Length; i++)
            if (MainCutMs[i] != 0)
            {
                h.Add(i + 5000);
                h.Add(MainCutMs[i]);
            }
        for (int i = 0; i < MagazineHeat.Length; i++)
            if (MagazineHeat[i] != 0 || MagazineFlood[i] || MagazineOrderMs[i] != 0 || MagazineWet[i] != 0)
            {
                h.Add(i + 6000);
                h.Add(MagazineHeat[i]);
                h.Add(MagazineFlood[i]);
                h.Add(MagazineOrderMs[i]);
                h.Add(MagazineWet[i]);
            }
        for (int i = 0; i < Fire.Length; i++)
            if (Fire[i].Phase != BurnPhase.None)
            {
                ref readonly var f = ref Fire[i];
                h.Add(i + 3000);
                h.Add((int)f.Phase);
                h.Add(f.Q);
                h.Add(f.E);
                h.Add(f.Involved);
                h.Add(f.T);
            }
        for (int i = 0; i < BreachM2.Length; i++)
            if (BreachM2[i] != 0 || CellFire[i] || CellOpened[i] || WaterM3[i] != 0)
            {
                h.Add(i);
                h.Add(BreachM2[i]);
                h.Add(WaterM3[i]);
                h.Add(CellFire[i]);
                h.Add(CellOpened[i]);
            }
    }
}
