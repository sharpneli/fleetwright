using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fleetwright.Shipgen;

/// <summary>A player's design, as its JSON gives it: everything optional (the designer fills in the defaults, many of
/// them per style), unknown keys kept in Extra so validation can name them and the design echoes back as given. The
/// keys are the properties in snake_case. docs/shipgen/README.md documents them.</summary>
public sealed record Design
{
    public string? Id { get; init; }
    public string? Name { get; init; }
    public string? Type { get; init; }
    public string? Style { get; init; }
    public double? SpeedKn { get; init; }
    public double? RangeNm { get; init; }
    public double? Funnels { get; init; }
    /// <summary>The deck finish ("wood", "steel"); warships pick by length when it's left out.</summary>
    public string? Deck { get; init; }
    public HullInput? Hull { get; init; }
    [JsonConverter(typeof(OneOrManyConverter<BatteryInput>))]
    public List<BatteryInput>? Main { get; init; }
    [JsonConverter(typeof(OneOrManyConverter<BatteryInput>))]
    public List<BatteryInput>? Secondary { get; init; }
    public TorpedoInput? Torpedoes { get; init; }
    public AaInput? Aa { get; init; }
    public ArmourInput? Armour { get; init; }
    public SuperstructureInput? Superstructure { get; init; }
    public MachineryInput? Machinery { get; init; }
    public CrewInput? Crew { get; init; }
    public FireControlInput? FireControl { get; init; }
    public AviationInput? Aviation { get; init; }
    public CargoInput? Cargo { get; init; }
    public LookInput? Look { get; init; }
    /// <summary>Colour overrides for the drawing (the renderer's palette keys).</summary>
    public Dictionary<string, Paint>? Palette { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }

    /// <summary>The style's name: "warship" unless the design says otherwise.</summary>
    [JsonIgnore] public string StyleName => Style ?? "warship";

    // the hull the designer chose (set on the sized design, see ShipDesign.WithHull)
    [JsonIgnore] public double HullLength => Hull?.Length ?? throw new InvalidOperationException("the design isn't sized yet");
    [JsonIgnore] public double HullBeam => Hull?.Beam ?? throw new InvalidOperationException("the design isn't sized yet");
    [JsonIgnore] public double BlockCoefficient =>
        Hull?.BlockCoefficient ?? throw new InvalidOperationException("the design isn't sized yet");

    /// <summary>The main batteries, in the design's order (empty ones left out).</summary>
    [JsonIgnore] public IEnumerable<BatteryInput> MainBatteries => Main ?? [];

    public static Design Parse(string json) =>
        JsonSerializer.Deserialize(json, ShipgenJson.Default.Design) ?? throw new JsonException("the design is null");

    public static Design Load(string path) => Parse(File.ReadAllText(path));

    public string ToJson(bool indented = false) =>
        JsonSerializer.Serialize(this, indented ? ShipgenJson.Indented.Design : ShipgenJson.Default.Design);
}

public sealed record HullInput
{
    public double? Length { get; init; }
    public double? Beam { get; init; }
    public double? BlockCoefficient { get; init; }
    public double? Freeboard { get; init; }
    public ConstructionInput? Construction { get; init; }
    public PlatingInput? Plating { get; init; }
    public List<RaisedInput>? Raised { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed record ConstructionInput
{
    public string? Name { get; init; }
    public double? YieldMpa { get; init; }
    public double? JoinFactor { get; init; }
    public double? Standard { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed record PlatingInput
{
    public double? ShellMm { get; init; }
    public string? Material { get; init; }
    public double? DeckWoodMm { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>hull.raised: a raised stretch of hull from one anchor to another, decks high.</summary>
public sealed record RaisedInput
{
    public string? From { get; init; }
    public string? To { get; init; }
    public int? Decks { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>A gun battery: main batteries count turrets fore, aft, mid and wing; secondaries count mounts.</summary>
public sealed record BatteryInput
{
    public double? CalibreMm { get; init; }
    public double? CalibreLength { get; init; }
    public int? Barrels { get; init; }
    public double? ArmourMm { get; init; }
    public double? RoundsPerGun { get; init; }
    public string? Material { get; init; }
    // main batteries
    public int? Fore { get; init; }
    public int? Aft { get; init; }
    public int? Mid { get; init; }
    public int? Wing { get; init; }
    public Superfire? Superfire { get; init; }
    public bool? Echelon { get; init; }
    public bool? CrossDeck { get; init; }
    public string? AmidshipsStandsOn { get; init; }
    // secondary batteries
    public int? Count { get; init; }
    public int? PerSide { get; init; }
    public string? Where { get; init; }
    public string? Mount { get; init; }
    public string? Tier { get; init; }
    public string? StandsOn { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }

    /// <summary>How many turrets a main battery has: its end, midships and wing turrets (two to a pair).</summary>
    [JsonIgnore] public int Turrets => (Fore ?? 0) + (Aft ?? 0) + (Mid ?? 0) + 2 * (Wing ?? 0);

    /// <summary>A secondary battery's mounts: count, else two per side.</summary>
    [JsonIgnore] public int MountCount => Count ?? 2 * (PerSide ?? 0);

    /// <summary>A secondary battery's mounts per side: per_side, else half the count.</summary>
    [JsonIgnore] public int MountsPerSide => PerSide ?? MountCount / 2;
}

/// <summary>main[k].superfire: true (every turret of an end group steps up), false (none), or how many step up at
/// each end.</summary>
[JsonConverter(typeof(SuperfireConverter))]
public sealed record Superfire(bool? All, int? Fore, int? Aft);

public sealed record TorpedoInput
{
    public int? Mounts { get; init; }
    public int? Tubes { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed record AaInput
{
    public int? Heavy { get; init; }
    public int? Light { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed record ArmourInput
{
    public double? BeltMm { get; init; }
    public double? BeltBottomMm { get; init; }
    public double? BeltDepthM { get; init; }
    public double? BeltHeightM { get; init; }
    public double? BulkheadMm { get; init; }
    public double? TdsM { get; init; }
    public double? FlightDeckMm { get; init; }
    public List<ArmourDeckInput>? Decks { get; init; }
    public UpperBeltInput? UpperBelt { get; init; }
    public EndBeltsInput? EndBelts { get; init; }
    public SteeringBoxInput? SteeringBox { get; init; }
    /// <summary>The armour material per part (Armour.ARMOUR_PARTS).</summary>
    public Dictionary<string, string>? Materials { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed record ArmourDeckInput
{
    public int? Deck { get; init; }
    public double? Mm { get; init; }
    public string? Extent { get; init; }
    public string? Material { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed record UpperBeltInput
{
    public double? Mm { get; init; }
    public int? ToDeck { get; init; }
    public string? Extent { get; init; }
    public string? Material { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed record EndBeltsInput
{
    public EndBeltInput? Fore { get; init; }
    public EndBeltInput? Aft { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }

    public EndBeltInput? Of(string end) => end == "fore" ? Fore : Aft;
}

public sealed record EndBeltInput
{
    public double? Mm { get; init; }
    public double? TipMm { get; init; }
    public double? Reach { get; init; }
    public double? BulkheadMm { get; init; }
    public string? Material { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed record SteeringBoxInput
{
    public double? Mm { get; init; }
    public double? DeckMm { get; init; }
    public double? BulkheadMm { get; init; }
    public string? Material { get; init; }
    public string? DeckMaterial { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed record SuperstructureInput
{
    public double? TPerM2 { get; init; }
    public string? Material { get; init; }
    public double? PlatingMm { get; init; }
    public double? ControlMm { get; init; }
    public int? TowerLevels { get; init; }
    public int? DeckhouseLevels { get; init; }
    public bool? AftControl { get; init; }
    public int? LevelsOverBridge { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed record MachineryInput
{
    public TechInput? Tech { get; init; }
    public double? Stress { get; init; }
    public int? Shafts { get; init; }
    public int? UnitsPerShaft { get; init; }
    public string? Transmission { get; init; }
    public Arrangement? Arrangement { get; init; }
    public bool? CentrelineBulkhead { get; init; }
    public string? Bunkers { get; init; }
    public double? WingBunkerM { get; init; }
    public int? Rudders { get; init; }
    /// <summary>merchants: "amidships" or "aft".</summary>
    public string? Position { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>machinery.arrangement: a named arrangement ("grouped", "unit") or the rooms forward to aft.</summary>
[JsonConverter(typeof(ArrangementConverter))]
public sealed record Arrangement(string? Name, List<string>? Rooms)
{
    public override string ToString() => Name ?? $"[{string.Join(", ", Rooms ?? [])}]";
}

/// <summary>The plant's technology; the style's default tech fills in what it leaves out.</summary>
public sealed record TechInput
{
    public string? Name { get; init; }
    public string? Fuel { get; init; }
    public double? WeightKgPerKw { get; init; }
    public double? StressFloor { get; init; }
    public double? SfcGPerKwh { get; init; }
    public double? DensityTPerM3 { get; init; }
    public double? UnitMaxMw { get; init; }
    public UnitInput? Unit { get; init; }
    public double? BoilerFraction { get; init; }
    public double? CrewK { get; init; }
    public string? PartLoad { get; init; }
    public DraughtInput? Draught { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed record UnitInput
{
    public double? Mw { get; init; }
    public double? HeightM { get; init; }
    public double? WidthM { get; init; }
    public double? LengthM { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed record DraughtInput
{
    public string? System { get; init; }
    [JsonPropertyName("velocity_m_s")] public double? VelocityMS { get; init; }
    public double? ReachM { get; init; }
    public double? GasTempK { get; init; }
    public double? AirFuelRatio { get; init; }
    public double? NaturalFraction { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed record CrewInput
{
    public double? EnduranceDays { get; init; }
    public bool? Distiller { get; init; }
    public double? BufferDays { get; init; }
    public double? WaterLPerDay { get; init; }
    public double? BerthRatio { get; init; }
    public double? OfficerFraction { get; init; }
    public CrewStandardInput? Standard { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>A habitability standard (Crew.Standard has the presets); the style's preset fills in what it leaves
/// out.</summary>
public sealed record CrewStandardInput
{
    public string? Name { get; init; }
    public double? SleepRatingM2 { get; init; }
    public double? SleepCpoM2 { get; init; }
    public double? SleepOfficerM2 { get; init; }
    public double? MessSeatsPerMan { get; init; }
    public double? GalleyK { get; init; }
    public double? GalleyMinM2 { get; init; }
    public double? SanitaryM2 { get; init; }
    public double? SickbayBedsPerMan { get; init; }
    public double? WelfareM2 { get; init; }
    public double? ServicesK { get; init; }
    public double? PassageFactor { get; init; }
    public double? DeckHeightM { get; init; }
    public double? ProvisionsM3PerDay { get; init; }
    public double? WaterLPerDay { get; init; }
    public double? HotelFraction { get; init; }
    public double? CpoFraction { get; init; }
    public double? ToleranceDays { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed record FireControlInput
{
    public DirectorInput? Main { get; init; }
    public DirectorInput? Secondary { get; init; }
    public DirectorInput? Aa { get; init; }
    public double? SearchRadarT { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }

    public DirectorInput? Of(string battery) => battery switch { "main" => Main, "secondary" => Secondary, _ => Aa };
}

public sealed record DirectorInput
{
    public double? Directors { get; init; }
    public double? RangefinderM { get; init; }
    public double? ArmourMm { get; init; }
    public double? RadarT { get; init; }
    public double? ComputerT { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed record AviationInput
{
    public string? FlightDeck { get; init; }
    public double? Aircraft { get; init; }
    public double? AircraftT { get; init; }
    public int? HangarDecks { get; init; }
    public string? Hangar { get; init; }
    public int? Elevators { get; init; }
    public int? DeckEdgeElevators { get; init; }
    public int? Catapults { get; init; }
    public int? Cranes { get; init; }
    [JsonConverter(typeof(TextConverter))]
    public string? Number { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed record CargoInput
{
    public string? Kind { get; init; }
    public double? DeadweightT { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>The design's look: how its navy paints and builds it in an era (Fleetwright.Shipgen.Render.Looks).</summary>
public sealed record LookInput
{
    public string? Navy { get; init; }
    public string? Era { get; init; }
    /// <summary>A pennant number, drawn by looks with hull_number.</summary>
    [JsonConverter(typeof(TextConverter))]
    public string? Number { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>A palette entry: one colour (#rrggbb), or several (the superstructure levels, the camouflage colours).</summary>
[JsonConverter(typeof(PaintConverter))]
public sealed record Paint(IReadOnlyList<string> Colours, bool IsList)
{
    public static Paint Of(string colour) => new([colour], false);
    public static Paint Of(IEnumerable<string> colours) => new(colours.ToList(), true);

    /// <summary>The colour of a single-colour entry.</summary>
    public string Colour => Colours[0];

    public Paint Select(Func<string, string> f) => new(Colours.Select(f).ToList(), IsList);

    public string this[int i] => Colours[i];
    public int Count => Colours.Count;
}

public static class JsonExtraExtensions
{
    /// <summary>The unknown keys a JSON object held (none when it held none).</summary>
    public static IEnumerable<string> KeysOrEmpty(this Dictionary<string, JsonElement>? extra) => extra?.Keys ?? Enumerable.Empty<string>();
}
