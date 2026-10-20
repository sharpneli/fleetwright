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
    /// <summary>The materials the parts name, keyed by name (MaterialTemplates; docs/battle/shells-armour-damage.md P0):
    /// a name the table doesn't give takes the catalogue's block.</summary>
    public Dictionary<string, MaterialInput>? Materials { get; init; }
    public TorpedoInput? Torpedoes { get; init; }
    public AaInput? Aa { get; init; }
    public ArmourInput? Armour { get; init; }
    /// <summary>How the hull is divided (protection-plan.md H7).</summary>
    public SubdivisionInput? Subdivision { get; init; }
    public SuperstructureInput? Superstructure { get; init; }
    /// <summary>A warship's masts, fore to aft (the foremast by the bridge, the mainmast on aft control): each one's kind,
    /// height and top (docs/battle/fire-control.md F4). One not given takes the default (a pole, a tripod on a long ship,
    /// over the funnels, no top).</summary>
    public List<MastInput>? Masts { get; init; }
    public MachineryInput? Machinery { get; init; }
    public CrewInput? Crew { get; init; }
    /// <summary>How the fittings are mounted: their shock grade (Fittings; docs/battle/shells-armour-damage.md E0).</summary>
    public FittingsInput? Fittings { get; init; }
    /// <summary>What burns aboard: each kind of space's fire load as fitted and stripped for war, the paint's, and how
    /// fast the linings let a fire grow (Outfits; docs/battle/fire.md).</summary>
    public OutfitInput? Outfit { get; init; }
    /// <summary>What's built in against water: drainage pumps, portable pumps, the counterflooding valves' rate and her
    /// boundaries' tightness (DrainageTemplates; docs/battle/damage-control.md DC4).</summary>
    public DrainageInput? Drainage { get; init; }
    public FireControlInput? FireControl { get; init; }
    /// <summary>The fire-control tech blocks the batteries' fits name, keyed by name (FireControlTemplates;
    /// docs/battle/fire-control.md F3): a name the table doesn't give takes the catalogue's block.</summary>
    public Dictionary<string, FcTechInput>? FireControlTech { get; init; }
    /// <summary>The gun blocks the batteries name (`main[i].gun`, `secondary[i].gun`), keyed by name (GunTemplates): how
    /// their guns are worked, laid and charged. A name the table doesn't give takes the catalogue's block.</summary>
    public Dictionary<string, GunTechInput>? GunTech { get; init; }
    /// <summary>The torpedo blocks `torpedoes.torpedo` names, keyed by name (TorpedoTemplates); a name the table doesn't
    /// give takes the catalogue's block.</summary>
    public Dictionary<string, TorpedoTechInput>? TorpedoTech { get; init; }
    public AviationInput? Aviation { get; init; }
    public CargoInput? Cargo { get; init; }
    public LookInput? Look { get; init; }
    /// <summary>Colour overrides for the drawing (the renderer's palette keys).</summary>
    public Dictionary<string, Paint>? Palette { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }

    /// <summary>Not input: what the format's upgrade couldn't tell from an older file and took a default for
    /// (DesignFormat), for the report's warnings.</summary>
    [JsonIgnore] public IReadOnlyList<string>? UpgradeWarnings { get; init; }

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
    public static Design Parse(string json)
    {
        var warnings = new List<string>();
        var d = DesignFormat.Upgrade(json, warnings).Deserialize(ShipgenJson.Default.Design) ?? throw new JsonException("the design is null");
        return warnings.Count > 0 ? d with { UpgradeWarnings = warnings } : d;
    }

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
    /// <summary>How the shell plating is joined: riveted or welded (Joint; welded when absent).</summary>
    public string? Shell { get; init; }
    /// <summary>How the internals (decks, bulkheads, framing) are joined: riveted or welded (welded when absent).</summary>
    public string? Internals { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>fittings: how the ship's gear is mounted. Room for the service networks' lock-in, selectivity and LVR.</summary>
public sealed record FittingsInput
{
    /// <summary>G0 unsecured, G1 secured, G2 shock-tested, G3 post-war (ShockGrade; G0 when absent).</summary>
    public string? ShockGrade { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>outfit: what burns aboard (docs/battle/fire.md; damage/17 §2). The fire loads as the ship is fitted and
/// as she's stripped for war (her orders before action pick one), the paint on the steel, and the growth factor over a
/// furnished room's "medium" t² fire (varnished wood 2, bare steel and retardant fabric 0.7). A key not given takes the
/// template's for the navy and year (Outfits).</summary>
public sealed record OutfitInput
{
    /// <summary>The template it was made from, a label only.</summary>
    public string? Name { get; init; }
    public FireLoadsInput? Fitted { get; init; }
    public FireLoadsInput? Stripped { get; init; }
    /// <summary>The paint's load, MJ per m² of deck (about 2.5 m² of bulkhead and deckhead each): on every space below
    /// the weather deck and in the superstructure.</summary>
    public double? PaintMjM2 { get; init; }
    public double? Growth { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>A design's drainage (docs/battle/damage-control.md DC4), every number optional: a number not given is its
/// default template's for the plant's year (DrainageTemplates).</summary>
public sealed record DrainageInput
{
    /// <summary>The template it was made from, a label only.</summary>
    public string? Name { get; init; }
    /// <summary>The fixed drainage pumps, tonnes an hour for each 1,000 t of displacement, at full power.</summary>
    [JsonPropertyName("pumps_tph_per_1000t")] public double? PumpsTphPer1000T { get; init; }
    /// <summary>The portable pumps carried and each one's rate, t/h (no power needed).</summary>
    public int? PortablePumps { get; init; }
    public double? PortableTph { get; init; }
    /// <summary>How fast the counterflooding valves fill the tanks on the high side, tonnes a minute for each 10,000 t of
    /// displacement (0: none).</summary>
    [JsonPropertyName("counterflood_tpm_per_10000t")] public double? CounterfloodTpmPer10000T { get; init; }
    /// <summary>The intact boundaries' leak, × the base: 1 air-tested, more for glands and penetrations never tested.</summary>
    public double? Tightness { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>Fire loads, MJ per m² of deck, by kind of space: messdecks and berthing (quarters), officers' cabins and
/// wardroom, offices and control spaces, stores, machinery spaces (lagging, oil film, rags), and what burns on the weather
/// deck (boats, hammocks, gear, the top of the planking).</summary>
public sealed record FireLoadsInput
{
    public double? Quarters { get; init; }
    public double? Cabins { get; init; }
    public double? Offices { get; init; }
    public double? Stores { get; init; }
    public double? Machinery { get; init; }
    public double? Deck { get; init; }
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
    /// <summary>A rangefinder's base in each of its turrets, m (fire-control.md F4b: across the gunhouse's rear in a hood,
    /// seeing only its turret's arcs); 0 or none: no rangefinder. Armoured and dual-purpose gunhouses only.</summary>
    public double? RangefinderM { get; init; }
    /// <summary>How its guns are housed (docs/battle/magazines.md V1, <see cref="Housings"/>): "barbette", "gunhouse" or
    /// "shield", at any calibre. None: the suggestion by calibre. A casemate battery's position says it instead.</summary>
    public string? Housing { get; init; }
    /// <summary>The gun's muzzle velocity with its heaviest shell, m/s (Ammunition.DefaultMuzzleVelocity when absent).</summary>
    public double? MuzzleVelocityMs { get; init; }
    /// <summary>Its gun block, by name (gun_tech, else the catalogue's: GunTemplates): its reload, training, elevation,
    /// pattern and propellant. None: <see cref="GunTemplates.Default"/>.</summary>
    public string? Gun { get; init; }
    /// <summary>The loadout: the ammunition blocks it carries and their shares of the rounds (none: the sim's placeholder
    /// by the look, until the default template).</summary>
    public List<ShellEntryInput>? Shells { get; init; }
    /// <summary>Not input: the loadout's mean shell mass, kg, stamped on by ShipDesign.Build (Ammunition.WithShellMasses)
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
/// muzzle_velocity_ms give this battery's own shell over the block's k·D³ and the fourth-root rule, and fuze_delay_s its
/// base fuze's delay over the block's (the navy set it for its own fights' ranges; a player sets it for theirs).</summary>
public sealed record ShellEntryInput
{
    public string? Ammo { get; init; }
    public double? Share { get; init; }
    public double? MassKg { get; init; }
    public double? MuzzleVelocityMs { get; init; }
    public double? FuzeDelayS { get; init; }
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
    /// <summary>The filler's share alongside the cylindrical body (the rest in the ogive and the base): the body's own
    /// charge-to-metal ratio throws the side spray (13 §2.5: AP 0.90, common 0.85, HE 0.75 [EST]).</summary>
    public double? Body { get; init; }
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

/// <summary>A material (docs/shipgen/material-templates.md): its density, t/m³ (what its plates weigh), and its resistance as a share of the same thickness of the penetration formula's plate (q, against an
/// 8" capped shell: WWII STS 1); for face-hardened plate the calibre scaling past 8" (q_scale, k: q·(D/8")^−k) and the
/// face (its share of the plate, and the thinnest plate made so), for homogeneous plate its elongation (under 25%, it
/// loses to shells over 8": Okun's M79 penalty). The rules are the sim's (MaterialRow). The fields it doesn't give are
/// the catalogue's block of the same name, else steel's.</summary>
public sealed record MaterialInput
{
    public double? DensityTPerM3 { get; init; }
    public double? Q { get; init; }
    public double? QScale { get; init; }
    public FaceInput? FaceHardened { get; init; }
    public BackInput? Back { get; init; }
    public double? ElongationPct { get; init; }
    /// <summary>How fragments see it (THOR's constants, docs/battle/shells-armour-damage.md E1): mild (structural steel,
    /// iron, and wood or cement by their mass), ductile (homogeneous armour), fh (face-hardened) or brittle (compound,
    /// cast).</summary>
    public string? Fragments { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>materials.NAME.back: where the plate's back gives (docs/battle/shells-armour-damage.md P4; damage/14 §2.1), as
/// u = V/V_L of a shell it stops: spall comes off from spall_u, and from holed_u the plate is holed by a shell that
/// breaks in it (the holing limit: face-hardened plate 2-12% below the Navy limit). By class: face-hardened 0.85/0.94,
/// homogeneous and structural 0.90/1.00 (holed only at the limit), brittle (compound, cast) 0.75/0.95 [EST].</summary>
public sealed record BackInput
{
    public double? SpallU { get; init; }
    public double? HoledU { get; init; }
    /// <summary>The share of a spall cloud that leaves as one big piece (the plug or the scab) once its severity is 0.5 or
    /// more (E3; damage/14 §3.2: 0.5, brittle plate 0.3) [EST].</summary>
    public double? Big { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>materials.NAME.face_hardened: the hard face's share of the plate's thickness, the thinnest plate made
/// face-hardened, mm (under max(it, 0.3 calibres) the plate has no face-hardened advantage), and whether the face
/// shatters a bare-nosed steel shell (true when not given; compound's steel face shatters only chilled shot, which its
/// own body's limits break).</summary>
public sealed record FaceInput
{
    public double? Share { get; init; }
    public double? MinMm { get; init; }
    public bool? Shatters { get; init; }
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
    /// <summary>The torpedo, by name (torpedo_tech, else the catalogue's: TorpedoTemplates). None:
    /// <see cref="TorpedoTemplates.Default"/>.</summary>
    public string? Torpedo { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>gun_tech.NAME: a gun block (GunTemplates; DesignerPlaceholders.Gun, Charge and Pattern read it), calibre-free
/// like machinery.tech: its reload as a share of the placeholder rule's; a heavy gun's slowing at 300 mm, for one of 40
/// calibres or more, under 40 and under 30; its training as a share of the rule's, and a dual-purpose mount's on top; its
/// highest elevation, degrees, and a dual-purpose mount's; its full-salvo pattern, % of the range at 305 mm, the power of
/// 305/d it goes by and a light gun's (under 280 mm); its charge's propellant (one of Propellants.Names), and the
/// length under which it fires black powder (0 none).</summary>
public sealed record GunTechInput
{
    public double? ReloadShare { get; init; }
    public double? HeavySlow { get; init; }
    [JsonPropertyName("heavy_slow_under_40_cal")] public double? HeavySlowUnder40Cal { get; init; }
    [JsonPropertyName("heavy_slow_under_30_cal")] public double? HeavySlowUnder30Cal { get; init; }
    public double? TrainShare { get; init; }
    public double? DpTrainShare { get; init; }
    public double? MaxElevationDeg { get; init; }
    public double? DpMaxElevationDeg { get; init; }
    public double? PatternPct { get; init; }
    public double? PatternExp { get; init; }
    public double? PatternLightPct { get; init; }
    public string? Propellant { get; init; }
    public double? BlackPowderUnderCal { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>torpedo_tech.NAME: a torpedo block (TorpedoTemplates; DesignerPlaceholders.Torpedo reads it), calibre-free: a
/// 21"'s fast and slow settings (kn, m), the share of the warhead that's TNT's worth, a deck mount's training (°/s) and
/// reload (s), a submerged tube's reload (s), its gyro's angle (degrees), and its shares that fail on the run and that
/// don't go off.</summary>
public sealed record TorpedoTechInput
{
    public double? FastKn { get; init; }
    public double? FastRangeM { get; init; }
    public double? SlowKn { get; init; }
    public double? SlowRangeM { get; init; }
    public double? ChargeShare { get; init; }
    public double? MountTrainDegS { get; init; }
    public double? MountReloadS { get; init; }
    public double? TubeReloadS { get; init; }
    public double? GyroDeg { get; init; }
    public double? Fail { get; init; }
    public double? Dud { get; init; }
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
    /// <summary>The belt beside the magazines (protection-plan.md H5; null: belt_mm), and how far the belt runs:
    /// "citadel" (the default) or "machinery" (a treaty cruiser's, the magazines boxed instead).</summary>
    public double? BeltMagazinesMm { get; init; }
    public string? BeltExtent { get; init; }
    /// <summary>The belt's lean off the upright, its top outboard (protection-plan.md H6; null or 0: upright), and where it
    /// stands: "external" (the default: on the side) or "internal" (inside the shell, Armour.InternalBeltInsetM in at its
    /// top and further in below by its lean).</summary>
    public double? BeltInclineDeg { get; init; }
    public string? BeltPosition { get; init; }
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
    /// <summary>An armoured longitudinal wall behind the belt, each side over the citadel (protection-plan.md H4).</summary>
    public InnerBulkheadInput? InnerBulkhead { get; init; }
    /// <summary>An armoured box round each run of magazines (protection-plan.md H5).</summary>
    public MagazineBoxInput? MagazineBox { get; init; }
    /// <summary>The armour material per part (Armour.ArmourParts).</summary>
    public Dictionary<string, string>? Materials { get; init; }
    /// <summary>The armoured gratings closing the uptakes' openings in the armour decks, as a share of the deck's plate:
    /// what a shell meets there (docs/battle/shells-armour-damage.md P7; null: Armour.GratingShareDefault, O.S. 751's
    /// holed grating worth 0.4 of its depth in solid plate).</summary>
    public double? Gratings { get; init; }
    /// <summary>The backing behind the belt and its zones (P7): Littorio's oak, US Class A's concrete, the ironclads'
    /// teak. The upper and end belts take it unless they give their own.</summary>
    public BackingInput? Backing { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>A side armour's backing (P7): a layer of a material (wood, cement) mm thick behind the plate. Weight, and a
/// near-zero plate in a shell's path (its material's q: wood 0.01, cement 0.04); what catches spall comes with E3.</summary>
public sealed record BackingInput
{
    public string? Material { get; init; }
    public double? Mm { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed record ArmourDeckInput
{
    public int? Deck { get; init; }
    public double? Mm { get; init; }
    /// <summary>A citadel deck's plate over the magazines (protection-plan.md H5; null: mm).</summary>
    public double? MagazinesMm { get; init; }
    /// <summary>Its slopes (protection-plan.md H6): each side the deck turns down at SlopeDeg from the horizontal (null:
    /// Armour.SlopeDegDefault) to meet the side at the belt's lower edge, SlopeMm thick (null or 0: flat to the side).</summary>
    public double? SlopeMm { get; init; }
    public double? SlopeDeg { get; init; }
    public string? Extent { get; init; }
    public string? Material { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>subdivision: whether the main bulkheads have doors below the damage-control deck (null: the style's way,
/// merchants yes, warships no). Doors make a ship easier to live in and are the classic path for flooding to spread
/// when they stand open (Victoria, Audacious); without them the spaces down there are reached from above.</summary>
public sealed record SubdivisionInput
{
    public bool? DoorsBelow { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed record UpperBeltInput
{
    public double? Mm { get; init; }
    public int? ToDeck { get; init; }
    public string? Extent { get; init; }
    public string? Material { get; init; }
    /// <summary>Its backing (null: the belt's, armour.backing).</summary>
    public BackingInput? Backing { get; init; }
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
    /// <summary>Its backing (null: the belt's, armour.backing).</summary>
    public BackingInput? Backing { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>The inner line behind the belt (protection-plan.md H4): an armoured longitudinal wall from the inner bottom to
/// the citadel's armour deck, each side. It is the wall a TDS (its holding bulkhead), wing bunkers or a magazine's wing
/// already put there, or one InboardM in from the side (null: the magazine wing's width). Mm over the citadel,
/// MagazinesMm beside the magazines (null: Mm); 0 for none.</summary>
public sealed record InnerBulkheadInput
{
    public double? Mm { get; init; }
    public double? MagazinesMm { get; init; }
    public double? InboardM { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>An armoured box round each run of magazines (protection-plan.md H5; a treaty cruiser's, an up-armoured
/// destroyer's): its sides on the magazines' wing walls, its crown on their crown, its ends, and a floor plate on the inner
/// bottom (against mines); 0 for none.</summary>
/// <summary>A protection scheme (protection-plan.md H8; docs/shipgen/protection-templates.md, the designer's
/// Content/Templates/protection.json): the layout a family of ships shared, as the armour inputs it sets, applied to a
/// design by ProtectionSchemes.Apply. No year inside. Thicknesses that scale with the ship are shares of the design's
/// own; the rest (torpedo and splinter bulkheads, boxes) are typical plates. Null: left as the design has it.</summary>
public sealed record ProtectionSchemeInput
{
    public string? BeltPosition { get; init; }
    public double? BeltInclineDeg { get; init; }
    public string? BeltExtent { get; init; }
    /// <summary>The belt's lower edge as a share of its thickness (belt_bottom_mm): an internal belt's taper.</summary>
    public double? BeltBottomShare { get; init; }
    /// <summary>Slopes on the citadel's main armour deck (its thickest): their angle, and the flat's plate as a share
    /// of the slopes'. The deck's thickness goes to the slopes, the flat takes the share of it.</summary>
    public double? SlopeDeg { get; init; }
    public double? FlatShare { get; init; }
    /// <summary>A light armour deck one deck under the main armour deck, with slopes (the French "upper deck stops, lower deck
    /// catches").</summary>
    public LowerDeckScheme? LowerDeck { get; init; }
    public InnerBulkheadInput? InnerBulkhead { get; init; }
    public MagazineBoxInput? MagazineBox { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>A scheme's lower deck: its flat's and slopes' plates, mm, and the slopes' angle.</summary>
public sealed record LowerDeckScheme
{
    public double? Mm { get; init; }
    public double? SlopeMm { get; init; }
    public double? SlopeDeg { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed record MagazineBoxInput
{
    public double? SideMm { get; init; }
    public double? CrownMm { get; init; }
    public double? EndMm { get; init; }
    public double? FloorMm { get; init; }
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

/// <summary>masts[i]: a mast's kind (pole, tripod, cage, lattice, tower; none leaves its place empty), its masthead over
/// the main deck, m, and its top: none, a fighting top (open, for lookouts) or a spotting top (enclosed, for the
/// spotters), its floor over the main deck, m, its radius, m, and its tiers (platforms stacked, a pagoda's); and the
/// battery (main, secondary, aa) one of whose directors stands on its top (the top grows to hold it; a mast without one
/// gets a spotting top). Heights not given follow the ship (MastPlan: the top at 2.25 times the highest gun's height over
/// the water, the masthead a topmast over it and its director).</summary>
public sealed record MastInput
{
    public string? Kind { get; init; }
    public string? Director { get; init; }
    public double? HeightM { get; init; }
    public string? Top { get; init; }
    public double? TopM { get; init; }
    public double? TopRadiusM { get; init; }
    public int? Tiers { get; init; }
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
    /// <summary>The boilers' kind (<see cref="Powerplant.BoilerKinds"/>) and the engines' (<see cref="Powerplant.EngineKinds"/>):
    /// what a shell meets in the boiler and engine rooms (docs/battle/shell-contents.md C1b).</summary>
    public string? Boilers { get; init; }
    public string? Engines { get; init; }
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
    /// <summary>A rangefinder in a hood on after control's top and on the conning tower's roof, their bases, m (0 or none:
    /// none): control positions for the main battery (fire-control.md F4b).</summary>
    public double? AftRangefinderM { get; init; }
    public double? ConningTowerRangefinderM { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }

    public DirectorInput? Of(string battery) => battery switch { "main" => Main, "secondary" => Secondary, _ => Aa };
}

/// <summary>fire_control.BATTERY: its directors, their rangefinder's base and hood armour, the radar's weight, and its fit:
/// the tech blocks its fire control is (one optics, one computer, one mount; by name, from fire_control_tech or the
/// catalogue, FireControlTemplates). A battery with directors and no fit fires at the catalogue's default preset; its
/// computer stands in a transmitting station under the armour, weighing its block's mass (fire-control.md F4c). Its
/// sights: the mount block (one without director firing) its guns lay by when no director fires them, in local control or
/// with no director at all ("Open sights" if not given, as local control laid until 2026-10-18).</summary>
public sealed record DirectorInput
{
    public double? Directors { get; init; }
    public double? RangefinderM { get; init; }
    public double? ArmourMm { get; init; }
    public double? RadarT { get; init; }
    public List<string>? Fit { get; init; }
    public string? Sights { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>A fire-control tech block (docs/battle/fire-control.md F3; research/simulation/fire-control-designer.md §2):
/// one of optics, a computer or a mount, its physical numbers under a name (FireControlTemplates). A battery's fit names
/// one of each; a radar block joins them with the radar pass.</summary>
public sealed record FcTechInput
{
    public OpticsTechInput? Optics { get; init; }
    public ComputerTechInput? Computer { get; init; }
    public MountTechInput? Mount { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>What an optical works makes (designer §2.1): its kind (eye: no instrument; coincidence; stereo); a trained
/// rangetaker's acuity through it, arcseconds; the longest base its tube keeps true, m, and how a longer one delivers
/// (B_eff = rigid·(B/rigid)^flex_exp); the instruments' magnification and the highest at a full image; readings a
/// minute; coatings and a gyro-stabilised mounting; the bare instrument's mass per square metre of base, kg.</summary>
public sealed record OpticsTechInput
{
    public string? Kind { get; init; }
    public double? DeltaArcsec { get; init; }
    public double? RigidBaseM { get; init; }
    public double? FlexExp { get; init; }
    public double? Magnification { get; init; }
    public double? MMax { get; init; }
    public double? ReadingsPerMin { get; init; }
    public bool? Coated { get; init; }
    public bool? GyroStabilised { get; init; }
    public double? MassKgM2 { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>A fire-control computer (designer §2.3, the sim's ComputerRow): its tracker (judgement, rate_clock,
/// true_course, acceleration, turn_rate) and whether own ship's motion is fed to it; latency, s, and the target
/// acceleration it allows, m²/s³; the clock's limits (m/s, °/s; 0 none); the acceleration filter's memory, s, and size,
/// m/s²; the turn filter's turn-rate (rad²/s³) and speed (m²/s³) noise; whether it opens up on a manoeuvre; how often a
/// judgement looks again, s; the eye's first guess it starts from (kn, °, 1 sigma); the residual ballistic bias, % of
/// range and mil; the extra error while own ship turns, m; the first ladder step, m; the salvo-to-salvo error, % of
/// range. Then the designer's: its mass, t, crew and power, kW (the transmitting station's, F4).</summary>
public sealed record ComputerTechInput
{
    public string? Tracker { get; init; }
    public bool? HelmFree { get; init; }
    public double? LatencyS { get; init; }
    public double? Q { get; init; }
    public double? MaxRangeRateMs { get; init; }
    public double? MaxBearingRateDegS { get; init; }
    public double? AccelTauS { get; init; }
    public double? AccelSigmaMs2 { get; init; }
    public double? TurnQ { get; init; }
    public double? SpeedQ { get; init; }
    public bool? Detect { get; init; }
    public double? RejudgeS { get; init; }
    public double? PriorKn { get; init; }
    public double? PriorDeg { get; init; }
    public double? ResidPct { get; init; }
    public double? DeflBiasMil { get; init; }
    public double? OwnTurnKickM { get; init; }
    public double? FirstStepM { get; init; }
    public double? SalvoPct { get; init; }
    public double? MassT { get; init; }
    public double? Crew { get; init; }
    public double? PowerKw { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>How the guns are laid (designer §2.4): from a director (its laying error common to the salvo) or each gun by
/// its own sights; a stable vertical (no sea-state penalty, with ship motion); the laying error and the salvo's
/// deflection wander, mil.</summary>
public sealed record MountTechInput
{
    public bool? DirectorFiring { get; init; }
    public bool? StableVertical { get; init; }
    public double? LayMil { get; init; }
    public double? SalvoDeflMil { get; init; }
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

/// <summary>The design's look: how its navy paints and builds it in an era (Fleetwright.Shipgen.Render.Looks). Only the
/// drawing reads it: no number the designer or the sim works out may depend on it (the user, 2026-10-19). What a navy or
/// a period gives a ship is its own blocks (gun_tech, torpedo_tech, fire_control_tech, ammunition, outfit, ...).</summary>
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
