using System.Text.Json.Serialization;

namespace Fleetwright.Shipgen;

/// <summary>The designed ship (ShipDesign.Build): the design as given, the report, the hitboxes and what the renderer
/// draws from.</summary>
public sealed record Ship(Design Design, Report Report, Hitboxes Hitboxes, RenderData Render);

// ------------------------------------------------------------------ the render data

/// <summary>What the renderer draws from: the layout's spec, the main deck's height above the waterline, the mounts'
/// arcs, the static height-map columns (lowest first) and the style's summary lines.</summary>
public sealed record RenderData(RenderSpec Spec, double DeckM, List<RenderMount> Mounts, List<HeightColumn> Columns, List<string> Summary);

public sealed record RenderMount(string Id, string Kind, double Rest, List<double[]> Arcs, double[]? Traverse, double Top)
{
    /// <summary>"casemate" for a casemate gun.</summary>
    public string? Mount { get; init; }
}

/// <summary>A height-map column: Top metres above the waterline over a shape: "hull", "polygon" (Points), "rect" (X, Y,
/// W, H), "circle" (Cx, Cy, R) or "ellipse" (Cx, Cy, Rx, Ry).</summary>
public sealed record HeightColumn(double Top, string Shape)
{
    public List<Pt>? Points { get; init; }
    public double? X { get; init; }
    public double? Y { get; init; }
    public double? W { get; init; }
    public double? H { get; init; }
    public double? Cx { get; init; }
    public double? Cy { get; init; }
    public double? R { get; init; }
    public double? Rx { get; init; }
    public double? Ry { get; init; }
}

// ------------------------------------------------------------------ the report

public sealed class Report
{
    public string? Id { get; init; }
    public string? Name { get; init; }
    public bool Valid { get; init; }
    public required List<string> Errors { get; init; }
    public required List<string> Warnings { get; init; }
    /// <summary>The player's design, as given.</summary>
    public required Design Inputs { get; init; }
    public required Results Results { get; init; }
    public required PlantReport Plant { get; init; }
    public required HullReport Hull { get; init; }
    public CrewReport? Crew { get; init; }
    public required List<DirectorReport> FireControl { get; init; }
    public BridgeReport? Bridge { get; init; }
    public required OrderedDictionary<string, long> WeightGroupsT { get; init; }
    public required List<WeightReport> Weights { get; init; }
}

/// <summary>The headline numbers; the style adds its own (the optional ones at the end).</summary>
public sealed class Results
{
    public double LengthM { get; set; }
    public double BeamM { get; set; }
    public double BlockCoefficient { get; set; }
    public long StandardDisplacementT { get; set; }
    public long FullDisplacementT { get; set; }
    public double DraughtM { get; set; }
    public double DepthM { get; set; }
    public double FreeboardM { get; set; }
    public double PowerShp { get; set; }
    public long FuelT { get; set; }
    public long Crew { get; set; }
    public double GmFullM { get; set; }
    public double GmLightM { get; set; }
    public double RollPeriodS { get; set; }
    public long? WindageM2 { get; set; }
    public double? GaleHeelDeg { get; set; }
    public string? GaleHeelCondition { get; set; }
    public double? DeckEdgeDeg { get; set; }
    public long? DeckEdgeWindKn { get; set; }
    public double TrimM { get; set; }
    public double LcgM { get; set; }
    public double LcbM { get; set; }
    public double LayoutShiftM { get; set; }

    // carriers
    public double? Aircraft { get; set; }
    public long? AircraftCapacity { get; set; }
    public long? HangarAreaM2 { get; set; }
    public string? FlightDeck { get; set; }
    public double[]? FlightDeckM { get; set; }
    public double? FlightDeckHeightM { get; set; }

    // planing craft
    public double? VolumetricFroude { get; set; }
    public double? PowerToWeightHpPerT { get; set; }
    public string? PowerModel { get; set; }

    // merchants
    public double? CargoT { get; set; }
    public long? DeadweightT { get; set; }
    public string? CargoKind { get; set; }
    public long? Holds { get; set; }
    public string? MachineryPosition { get; set; }
}

/// <summary>The plant's static numbers for the game and how it sits in the hull.</summary>
public sealed class PlantReport
{
    public required string Name { get; init; }
    public required string Fuel { get; init; }
    public long RatedKw { get; init; }
    public double RatedShp { get; init; }
    public long ContinuousKw { get; init; }
    public double OverloadMax { get; init; }
    public long Shafts { get; init; }
    public long Units { get; init; }
    public double UnitMw { get; init; }
    public double WeightT { get; init; }
    public double SfcGPerKwh { get; init; }
    public required PartLoad PartLoad { get; init; }
    public required string Draught { get; init; }
    public double? NaturalFraction { get; init; }
    public double Stress { get; init; }
    public required string Transmission { get; init; }
    [JsonConverter(typeof(ArrangementConverter))]
    public required Arrangement Arrangement { get; init; }
    public long Crew { get; init; }
    public double MachineryLengthM { get; set; }
    public long BoilerRooms { get; set; }
    public long EngineRooms { get; set; }
    public long? Rows { get; set; }
    public double ProtrusionM { get; set; }
    public SpaceM SpaceM { get; set; } = new(0, 0);
    public long WingBunkersT { get; set; }
    public double EndBunkersM { get; set; }
    public long Funnels { get; set; }
    public double FunnelGasAreaM2 { get; set; }
    [JsonPropertyName("funnel_gas_velocity_m_s")]
    public double FunnelGasVelocityMS { get; set; }
    public double SmokeReachM { get; set; }
}

public sealed record SpaceM(double Width, double Height);

public sealed record PartLoad(string Curve, double[] Loads, double[] Multipliers, double OverloadPerTenth);

/// <summary>The hull structure and its girder amidships, for the damage model (only StructureT without the plate
/// model).</summary>
public sealed class HullReport
{
    public string? Construction { get; init; }
    public double StructureT { get; init; }
    public double? MinGaugeT { get; init; }
    public double? StrengthT { get; init; }
    public double? PlateMinMm { get; init; }
    public double? PlateStrengthMm { get; init; }
    public double? ShellPlatingT { get; init; }
    public Girder? Girder { get; init; }
}

public sealed record Girder(double AllowableStressMpa, double RequiredM4, double PlatingM4, double ArmourDecksM4);

public sealed record DirectorReport(string Id, string? Battery, double X, double Y, double EyeHeightM, double HorizonKm, double RangefinderM,
    double ArmourMm, double RadarT, double WeightT);

/// <summary>The navigating bridge's view (warships).</summary>
public sealed record BridgeReport(long Level, long TowerLevels, double EyeHeightM, double HorizonKm, bool SeesOverTurrets,
    long LevelToSeeOverTurrets);

public sealed record WeightReport(string Name, string Group, double T, double X, double Z);

// ------------------------------------------------------------------ the hitboxes

/// <summary>hitboxes.json: components with exact shapes in ship-local metres and heights above the main deck, the hull's
/// armour, plating and hydrostatics, and its subdivision.</summary>
public sealed class Hitboxes
{
    public string Units => "metres";
    public string Frame => "ship-local: origin = ship centre = sprite centre, +x toward bow, +y toward starboard; " +
                           "angles clockwise from dead ahead";
    public string Heights => "base/top/z are metres above the main deck (negative = below it)";
    public string TurretLocal => "turret 'local' polygons are in turret space (pivot at 0,0, barrels along +x); " +
                                 "rotate by the current turret angle, then add (x, y)";
    public double Length { get; init; }
    public double Beam { get; init; }
    public required Vertical Vertical { get; init; }
    public required List<Pt> Hull { get; init; }
    public required Hydrostatics Hydrostatics { get; init; }
    public required HullFormReport HullForm { get; init; }
    public required ArmourReport Armour { get; init; }
    public required HullPlates Plating { get; init; }
    public required List<Component> Components { get; init; }
    public required List<SubDeck> Decks { get; init; }
    public required List<Tier> Tiers { get; init; }
    public required List<Section> Sections { get; init; }
    public required List<Bulkhead> Bulkheads { get; init; }
    public required List<Cell> Cells { get; init; }
    public required List<Room> Rooms { get; init; }
}

public sealed class Vertical
{
    public double Keel { get; init; }
    public double Waterline { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public double? ArmourDeck { get; init; }
    public double Draught { get; init; }
    public double Depth { get; init; }
    public double Freeboard { get; init; }
    public List<RaisedReport>? Raised { get; init; }
}

public sealed record RaisedReport(string Id, double X0, double X1, double Top);

/// <summary>The full-load hydrostatics a game needs to settle, trim and heel a flooded ship by added weight.</summary>
public sealed class Hydrostatics
{
    public long DisplacementT { get; init; }
    public long VolumeM3 { get; init; }
    public double WaterplaneM2 { get; init; }
    public double Lcf { get; init; }
    public double Lcg { get; init; }
    public double Lcb { get; init; }
    public double Kg { get; init; }
    public double Kb { get; init; }
    public double GmT { get; init; }
    public double GmL { get; init; }
    [JsonPropertyName("i_t_m4")]
    public long ITM4 { get; init; }
    [JsonPropertyName("i_l_m4")]
    public long ILM4 { get; init; }
    public double TpcT { get; init; }
    public double MctTm { get; init; }
}

public sealed record HullFormReport(double MidshipCoefficient, double WaterplaneCoefficient, List<StationReport> Stations);

/// <summary>A hull station: its half-breadths Y at heights Z.</summary>
public sealed record StationReport(double X, double[] Z, double[] Y);

public sealed class ArmourReport
{
    public BeltReport? Belt { get; set; }
    public List<StrakeReport>? Strakes { get; set; }
    public List<ArmourBulkheadReport>? Bulkheads { get; set; }
    public List<ArmourDeckReport>? Decks { get; set; }
}

public sealed record BeltReport(double ThicknessMm, double X0, double X1, double Bottom, double Top)
{
    public string? Material { get; init; }
    public double? BottomMm { get; init; }
    public double? TaperFrom { get; init; }
}

public sealed record StrakeReport(string Id, string Kind, string Extent, double ThicknessMm, double X0, double X1, double Bottom, double Top)
{
    public double? TipMm { get; init; }
    public string? Material { get; init; }
}

public sealed record ArmourBulkheadReport(string Id, double X, double ThicknessMm, double Bottom, double Top, string? Material);

public sealed record ArmourDeckReport(string Deck, double ThicknessMm, string Extent, double X0, double X1, double Z, bool Main, bool Roof,
    string? Material);

/// <summary>One hitbox component. Kind says which it is and so which of the optional fields it has: a gun or torpedo
/// mount, barbette, superstructure block, conning tower, funnel, uptake, casing, deck, sponson, AA mount, hangar bay,
/// shaft, shaft alley, propeller or rudder.</summary>
public sealed class Component
{
    public required string Id { get; init; }
    public required string Kind { get; init; }
    public string? Type { get; init; }
    public string? Role { get; init; }
    public double? CalibreMm { get; init; }
    public double? CalibreLength { get; init; }
    public string? Shape { get; init; }
    public List<Pt>? Points { get; init; }
    public Rrect? Rrect { get; set; }
    public double? X { get; init; }
    public double? Y { get; init; }
    public double? Z { get; init; }
    public double? R { get; init; }
    public double[]? P0 { get; init; }
    public double[]? P1 { get; init; }
    public double Base { get; init; }
    public double Top { get; init; }
    public double? ArmourMm { get; set; }
    public TurretArmour? Armour { get; set; }
    public string? Material { get; set; }
    public double? PlateMm { get; set; }
    public double? WoodMm { get; set; }
    public double? BroadphaseR { get; init; }
    public bool? Rotating { get; init; }
    public double? RestDeg { get; init; }
    public List<double[]>? ArcsDeg { get; init; }
    public double[]? TraverseDeg { get; init; }
    public TurretLocal? Local { get; init; }
    public long? Rounds { get; set; }
    public long? ReadyRounds { get; set; }
    public double? ReadyT { get; set; }
    public long? Torpedoes { get; set; }
    public long? WarheadKg { get; set; }
    public string? Magazine { get; set; }
    public string? Mount { get; set; }
    public string? Barbette { get; set; }
    public string? Battery { get; set; }
    public double? RangefinderM { get; set; }
    public bool? Radar { get; set; }
    public long? Crew { get; set; }
    public List<string>? Smoke { get; set; }
    public List<string>? BoilerRooms { get; init; }
    public string? Funnel { get; init; }
    public string? Position { get; init; }
    public double? LeavesHullX { get; init; }
    public string? Propeller { get; init; }
    public string? EngineRoom { get; init; }
    public string? Alley { get; init; }
    public string? Shaft { get; init; }
    public double? DiameterM { get; init; }
    public double? AreaM2 { get; init; }
    public string? Steering { get; init; }
    public long? BattleCrew { get; set; }
}

public sealed record Rrect(double X0, double X1, double Y0, double Y1, double Rf, double Rb);

public sealed record TurretArmour(double Face, long Side, long Rear, long Roof);

/// <summary>A turret's polygons in turret space: pivot at 0,0, barrels along +x.</summary>
public sealed record TurretLocal(List<Pt> Body, List<List<Pt>> Parts, List<List<Pt>> Barrels);

// ------------------------------------------------------------------ the subdivision

/// <summary>The hull below the main deck as a grid of watertight cells, and the rooms that own them.</summary>
public sealed record SubdivisionData(List<SubDeck> Decks, List<Tier> Tiers, List<Section> Sections, List<Bulkhead> Bulkheads,
    List<Cell> Cells, List<Room> Rooms);

/// <summary>A deck of the subdivision, Z above the main deck; armoured ones have their plate (or Plates).</summary>
public sealed class SubDeck
{
    public required string Id { get; init; }
    public required string Kind { get; init; }
    public long? Deck { get; init; }
    public double Z { get; set; }
    public List<double[]>? Spans { get; init; }
    public double? ArmourMm { get; set; }
    public double? X0 { get; set; }
    public double? X1 { get; set; }
    public string? Material { get; set; }
    public List<DeckPlate>? Plates { get; set; }
    public double? PlateMm { get; set; }
    public double? PlateEndMm { get; set; }
    public double? WoodMm { get; set; }

    public sealed record DeckPlate(double ArmourMm, double X0, double X1)
    {
        public string? Material { get; init; }
    }
}

/// <summary>The space between two decks.</summary>
public sealed class Tier
{
    public required string Id { get; init; }
    public double Base { get; set; }
    public double Top { get; set; }
    public bool BelowWaterline { get; init; }
    /// <summary>How much of it is under the waterline, 0..1.</summary>
    public double Submerged { get; set; }
    public required string Floor { get; init; }
    public required string Ceiling { get; init; }
    public List<double[]>? Spans { get; init; }
}

public sealed record Section(string Id, double X0, double X1);

/// <summary>A transverse bulkhead (X) or a longitudinal one (Section, Side, Y, X0..X1).</summary>
public sealed class Bulkhead
{
    public required string Id { get; init; }
    public required string Kind { get; init; }
    public double? X { get; init; }
    public string? Section { get; init; }
    public string? Side { get; init; }
    public double? Y { get; init; }
    public double? X0 { get; init; }
    public double? X1 { get; init; }
    public double Base { get; set; }
    public double Top { get; init; }
    public double? ArmourMm { get; set; }
    public double? ArmourBottom { get; set; }
    public double? ArmourTop { get; set; }
    public string? ArmourMaterial { get; set; }
    public double? PlateMm { get; set; }
}

/// <summary>A watertight cell: one section x tier x band.</summary>
public sealed class Cell
{
    public required string Id { get; init; }
    public required string Section { get; init; }
    public required string Tier { get; init; }
    public required string Band { get; init; }
    public double X0 { get; set; }
    public double X1 { get; set; }
    public double Y0 { get; set; }
    public double Y1 { get; set; }
    public double Base { get; set; }
    public double Top { get; set; }
    public bool BelowWaterline { get; init; }
    public double VolumeM3 { get; set; }
    public bool? Citadel { get; set; }
    public List<double>? ArmourAboveMm { get; set; }
    public List<string?>? ArmourAboveMaterial { get; set; }
    public long? BeltMm { get; set; }
    public string? BeltMaterial { get; set; }
    public double? TdsM { get; set; }
    public long? Crew { get; set; }
    public long? BattleCrew { get; set; }
    public string Room { get; set; } = "";
    public List<string>? Also { get; set; }
    public double Permeability { get; set; }
    /// <summary>[cell id, what joins them: "open" (same room), a deck or a bulkhead].</summary>
    public List<string[]> Neighbours { get; set; } = [];
    /// <summary>The shafts and alleys that pass through.</summary>
    public List<string>? Through { get; set; }

    [JsonIgnore] internal int Si, Ti, T0;
    [JsonIgnore] internal double VUnder, VOver;
    [JsonIgnore] internal bool Merged;
}

/// <summary>A room: the cells it owns (and, Shared, one it shares with another room).</summary>
public sealed record Room
{
    public required string Id { get; init; }
    public required string Kind { get; init; }
    public List<string> Cells { get; set; } = [];
    public string? Fuel { get; init; }
    public double? Tonnes { get; set; }
    public string? Mount { get; init; }
    public List<string>? Mounts { get; init; }
    public bool? Shared { get; set; }
    public long? Crew { get; set; }
    public long? BattleCrew { get; set; }
    public List<string>? Shafts { get; set; }
    public List<string>? Rudders { get; set; }
    public double VolumeM3 { get; set; }
    public double X0 { get; set; }
    public double X1 { get; set; }
    public double Base { get; set; }
    public double Top { get; set; }
}
