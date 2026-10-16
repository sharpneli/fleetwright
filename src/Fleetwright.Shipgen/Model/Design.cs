using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fleetwright.Shipgen;

/// <summary>A player's design, as its JSON gives it: everything optional (the designer fills in the defaults, many of
/// them per style), unknown keys kept in Extra so validation can name them and the design echoes back as given. The
/// keys are the properties in snake_case. docs/shipgen/README.md documents them.</summary>
public sealed record Design
{
    /// <summary>The file format (DesignFormat): older files are upgraded as they're read.</summary>
    public int Version { get; init; } = DesignFormat.Current;
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
    /// <summary>The shell tech blocks the batteries' shells name, keyed by name (Ammunition; docs/battle/
    /// shells-armour-damage.md phase S).</summary>
    public Dictionary<string, AmmoInput>? Ammunition { get; init; }
    /// <summary>The explosives the fillers name, keyed by name (copied from the catalogue, Explosives.cs).</summary>
    public Dictionary<string, ExplosiveInput>? Explosives { get; init; }
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

    /// <summary>A design from its JSON, upgraded from an older format first (DesignFormat).</summary>
    public static Design Parse(string json) =>
        DesignFormat.Upgrade(json).Deserialize(ShipgenJson.Default.Design) ?? throw new JsonException("the design is null");

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
    public SectionInput? Section { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>hull.section: the hull's cross-section (Topside).</summary>
public sealed record SectionInput
{
    public string? Topside { get; init; }
    public double? Strength { get; init; }
    public double? Knuckle { get; init; }
    public string? Extent { get; init; }
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
    /// <summary>The gun's muzzle velocity with its heaviest shell, m/s (Ammunition.DefaultMuzzleVelocity when absent).</summary>
    public double? MuzzleVelocityMs { get; init; }
    /// <summary>The outfit: the ammunition blocks it carries and their shares of the rounds (none: the sim's placeholder
    /// by the look, until the default template).</summary>
    public List<ShellEntryInput>? Shells { get; init; }
    /// <summary>Not input: the outfit's mean shell mass, kg, stamped on by ShipDesign.Build (Ammunition.WithShellMasses)
    /// for the magazines' weight; null outside a build.</summary>
    [JsonIgnore] public double? ShellKg { get; init; }
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

/// <summary>A battery's shells[k]: an ammunition block by name and its share of the rounds; mass_kg and
/// muzzle_velocity_ms give this battery's own shell over the block's k·D³ and the fourth-root rule.</summary>
public sealed record ShellEntryInput
{
    public string? Ammo { get; init; }
    public double? Share { get; init; }
    public double? MassKg { get; init; }
    public double? MuzzleVelocityMs { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>ammunition.NAME: a shell tech block, calibre-free like machinery.tech (docs/battle/
/// shells-armour-damage.md, "Ammunition, per battery"; docs/shipgen/shell-templates.md has the catalogue). Every share
/// is of the shell's mass, every threshold in calibres or T/D, so one block serves a navy's family of guns.</summary>
public sealed record AmmoInput
{
    /// <summary>ap, common or he.</summary>
    public string? Kind { get; init; }
    /// <summary>Mass = k·D³, D in m, kg/m³ (1 lb/in³ = 27,680).</summary>
    public double? MassKKgM3 { get; init; }
    /// <summary>The nose's ogive radius in calibres (2 crh pre-1909 RN to 9 crh the USN Mk 8): penetration ±10-15%.</summary>
    public double? NoseCrh { get; init; }
    /// <summary>The drag form factor over the standard Cd(Mach) curve.</summary>
    public double? FormFactor { get; init; }
    public CapInput? Cap { get; init; }
    /// <summary>A false ogive over the penetrating nose, lost on any impact.</summary>
    public bool? Windscreen { get; init; }
    public BodyInput? Body { get; init; }
    public FillerInput? Filler { get; init; }
    public SteelInput? Steel { get; init; }
    public CasingInput? Casing { get; init; }
    public FuzeInput? Fuze { get; init; }
    /// <summary>TRANSITIONAL (S0 to P3): the sim's outcome-table row, rn1916, km1916, rn1918, ww2 or ijn_t91.</summary>
    public string? Outcomes { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>ammunition.NAME.cap: none, soft, hard, hard2 or break_away (the diving shell's), and its share of the
/// mass.</summary>
public sealed record CapInput
{
    public string? Kind { get; init; }
    public double? Share { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>ammunition.NAME.body: when it breaks up (past break_ob_deg of obliquity on a plate of break_t_d calibres or
/// more) and its quality, the multiplier on what it defeats (armor/00 §5.4: 0.85-1.11).</summary>
public sealed record BodyInput
{
    public double? BreakObDeg { get; init; }
    [JsonPropertyName("break_t_d")] public double? BreakTD { get; init; }
    public double? Quality { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>ammunition.NAME.filler: its share of the mass, the explosive (a name in the design's `explosives` or the
/// catalogue, or a block, whose numbers override the named one's) and the booster (basic or ce) that picks its
/// full-detonation rate.</summary>
public sealed record FillerInput
{
    public double? Share { get; init; }
    [JsonConverter(typeof(ExplosiveRefConverter))] public ExplosiveInput? Explosive { get; init; }
    public string? Booster { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>An explosive (docs/shipgen/explosive-templates.md): its TNT equivalent for the blast (re), the heats of
/// detonation and combustion, MJ/kg, the air it burns, m³/kg, and the afterburn's efficiency (eta) for a closed space,
/// the Gurney constant, m/s, and brisance (B_TNT/B_Mott) for the fragments, the in-plate rate at the limit, the
/// full-detonation rates by booster, the fire start's multiplier, and the year it came in (a label; the sim never reads
/// it). As a filler's `explosive`, its name picks the design's or the catalogue's block and what it gives overrides.</summary>
public sealed record ExplosiveInput
{
    public string? Name { get; init; }
    public double? Re { get; init; }
    public double? QDet { get; init; }
    public double? QComb { get; init; }
    public double? AirM3Kg { get; init; }
    public double? Eta { get; init; }
    public double? GurneyMs { get; init; }
    public double? Brisance { get; init; }
    public double? InPlate { get; init; }
    public FullInput? Full { get; init; }
    public double? Fire { get; init; }
    public int? From { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>An explosive's full-detonation rate with a basic booster and with a CE (tetryl) one; the rest go low-order
/// or dud.</summary>
public sealed record FullInput
{
    public double? Basic { get; init; }
    public double? Ce { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>ammunition.NAME.steel: the body's steel as Mott's fragment-size (B) and Gurney's speed multipliers: forged
/// carbon 1/1, Ni-Cr AP 1.25/1, cast steel 0.85/0.95, cast iron 0.6/0.8 (13 §3.2).</summary>
public sealed record SteelInput
{
    public double? Size { get; init; }
    public double? Speed { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>ammunition.NAME.casing: how the casing breaks, as shares of its metal: the nose slug, the ogive's spray, the
/// body's side spray, the base plug and the tail (13 §2.5).</summary>
public sealed record CasingInput
{
    public double? Nose { get; init; }
    public double? Ogive { get; init; }
    public double? Body { get; init; }
    public double? Plug { get; init; }
    public double? Tail { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>ammunition.NAME.fuze: base or nose and the delay once it starts, s; the plate that starts it (start_cal
/// calibres at normal, times `oblique` at high obliquity), whether thin plates in quick succession add up (plates:
/// single or two), the delay's scatter (a share), the blinds and duds (a share), and on the water how long a nose fuze
/// takes, ms, and whether it fires at all (a share; 15 §6.2).</summary>
public sealed record FuzeInput
{
    public string? Position { get; init; }
    public double? DelayS { get; init; }
    public double? StartCal { get; init; }
    public double? Oblique { get; init; }
    public string? Plates { get; init; }
    public double? Scatter { get; init; }
    public double? Blind { get; init; }
    public double? WaterMs { get; init; }
    public double? WaterFires { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>main[k].superfire: true (every turret of an end group steps up), false (none), or how many step up at
/// each end.</summary>
[JsonConverter(typeof(SuperfireConverter))]
public sealed record Superfire(bool? All, int? Fore, int? Aft);

public sealed record TorpedoInput
{
    public int? Mounts { get; init; }
    public int? Tubes { get; init; }
    /// <summary>Fixed tubes below the waterline (SubmergedTubes).</summary>
    public SubmergedInput? Submerged { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>torpedoes.submerged: beam tubes per side, bow and stern tubes, and the torpedoes each tube carries (the one
/// in it included).</summary>
public sealed record SubmergedInput
{
    public int? PerSide { get; init; }
    public int? Bow { get; init; }
    public int? Stern { get; init; }
    public int? TorpedoesPerTube { get; init; }
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
    /// <summary>The TDS's layers each side (protection-plan.md H3): 1 (the default) is one space behind one wall, the
    /// holding bulkhead; more part it with thinner walls, evenly from the holding bulkhead out to the shell.</summary>
    public int? TdsLayers { get; init; }
    /// <summary>The holding bulkhead's plate (null: all the TDS's steel with one layer, half of it with more), and each
    /// layer wall's (null: the rest of the steel shared among them), mm. The steel is Armour.TdsMmPerM per metre of depth
    /// unless both are given.</summary>
    public double? TdsHoldingMm { get; init; }
    public double? TdsLayerMm { get; init; }
    /// <summary>The wing of stores between a magazine and the side, each side (protection-plan.md H2); null: 0.1 of the
    /// beam, 0: none. Where a TDS or wing bunkers already stand there, they are the wing.</summary>
    public double? MagazineWingM { get; init; }
    public double? FlightDeckMm { get; init; }
    public List<ArmourDeckInput>? Decks { get; init; }
    public UpperBeltInput? UpperBelt { get; init; }
    public EndBeltsInput? EndBelts { get; init; }
    public SteeringBoxInput? SteeringBox { get; init; }
    /// <summary>The armour material per part (Armour.ArmourParts).</summary>
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
