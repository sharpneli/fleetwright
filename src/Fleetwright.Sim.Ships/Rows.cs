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
}

public readonly record struct BarbetteRow(int Component, int Mount, double X, double Y, double R);

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
}

public enum Extent : byte
{
    Citadel,
    Fore,
    Aft,
    Full,
    Steering,
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
}

/// <summary>An armoured transverse bulkhead: ThicknessMm from Top down, LowerMm under LowerTop.</summary>
public readonly record struct ArmourBulkheadRow(double X, double ThicknessMm, double Bottom, double Top, int Material, double LowerMm, double LowerTop);

/// <summary>An armour deck's plate: Deck (an index) at Z over X0..X1.</summary>
public readonly record struct ArmourDeckRow(int Deck, double ThicknessMm, Extent Extent, double X0, double X1, double Z, bool Main, bool Roof, int Material);

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
}
