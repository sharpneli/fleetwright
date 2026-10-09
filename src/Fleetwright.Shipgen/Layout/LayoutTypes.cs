using System.Text.Json.Serialization;

namespace Fleetwright.Shipgen;

/// <summary>A turret (or torpedo mount) type: its look and size, the guns it carries and how many rounds each.
/// Shape: "bb" (armoured turret), "dp" (dual purpose), "open", "casemate", "torp" (trainable tubes) or "tube" (a fixed
/// tube).</summary>
public sealed record TurretType : IPyValue
{
    public required string Desc { get; init; }
    public required string Shape { get; init; }
    public required double R { get; init; }
    public required long Barrels { get; init; }
    public required double BarrelLen { get; init; }
    public required double BarrelW { get; init; }
    public required double Spacing { get; init; }
    public bool? Centered { get; init; }
    public bool? Barbette { get; init; }
    public bool? FixedTube { get; init; }
    public required double CalibreMm { get; init; }
    public double? CalibreLength { get; init; }
    public double? RoundsPerGun { get; init; }

    /// <summary>Does a mount of this type stand on a barbette?</summary>
    [JsonIgnore] public bool HasBarbette => Barbette ?? true;

    [JsonIgnore] public bool IsFixedTube => FixedTube ?? false;

    public object? ToPy() => JsonBridge.ToPy(this, ShipgenJson.Default.TurretType);
}

/// <summary>A gun or torpedo mount as the layout places it. Base and Top are its heights above the main deck; Rest its
/// bearing at rest (degrees clockwise from ahead); Arcs and Traverse are set once the layout is done.</summary>
public sealed class Mount
{
    public required string Id;
    /// <summary>"main", "secondary" or "torpedo".</summary>
    public required string Kind;
    public required string Type;
    public required TurretType T;
    public double X, Y;
    public long Level;
    public double Base, Top;
    public double Rest;
    public double ArmourMm;
    /// <summary>A fixed tube's bearing.</summary>
    public double? Fixed;
    public bool SideMount, Casemate, Wing, Echelon, CrossDeck, Midships;
    /// <summary>"beam" for a turret that fires on the beams only (it can't fire over the turret ahead).</summary>
    public string? ArcRole;
    public string? Battery;
    public string? Material;
    /// <summary>A wing turret's magazine end: "fore" or "aft".</summary>
    public string? MagazineEnd;
    /// <summary>The magazine its ammunition is stowed in.</summary>
    public string? Magazine;
    /// <summary>The draw order (equal bases share one).</summary>
    public long Z;
    public List<double[]> Arcs = [];
    public double[]? Traverse;
}

/// <summary>A superstructure block (or a director standing on a roof as a block of its own): a rounded rectangle from X0
/// to X1, W wide about Y, or its own outline (Points, then X0..X1 and W are its box). It stands Level levels up from Z0
/// above the main deck.</summary>
public sealed class Block : IPyValue
{
    [JsonIgnore] public required string Id;
    /// <summary>"superstructure" or "director".</summary>
    [JsonIgnore] public required string Kind;
    /// <summary>One of Layout.BLOCK_ROLES.</summary>
    [JsonIgnore] public required string Role;
    [JsonIgnore] public bool Office;
    public double X0 { get; set; }
    public double X1 { get; set; }
    public double Y { get; set; }
    public double W { get; set; }
    public long Level { get; set; }
    public double Rf { get; set; }
    public double Rb { get; set; }
    public List<Pt>? Points { get; set; }
    public double? Area { get; set; }
    /// <summary>What it stands on, above the main deck (null: the main deck).</summary>
    public double? Z0 { get; set; }
    /// <summary>The drawing layer ("upper" for a block standing on something).</summary>
    public string? Layer { get; set; }
    public BlockDirector? Director { get; set; }
    /// <summary>Its wall plating, mm.</summary>
    [JsonIgnore] public double? PlateMm;
    /// <summary>The convex outline it was cut from, and its notches (for the levels stacked on it).</summary>
    [JsonIgnore] public List<Pt>? Support;
    [JsonIgnore] public List<(double N0, double N1, double H)>? Notches;
    [JsonIgnore] internal Layout.Slabs? Slabs;

    [JsonIgnore] public double Base => (Z0 ?? 0) + Layout.LEVEL_H * (Level - 1);
    [JsonIgnore] public double TopZ => (Z0 ?? 0) + Layout.LEVEL_H * Level;

    public object? ToPy() => JsonBridge.ToPy(this, ShipgenJson.Default.Block);
}

/// <summary>A director's block: its battery, the rangefinder's base, whether it carries radar, and the level it
/// stands on.</summary>
public sealed record BlockDirector(string Battery, double RangefinderM, bool Radar, long On);

/// <summary>A funnel: L long and W wide at X, Y, its foot Z0 above the main deck (null: on the deck), its top, and the
/// boiler rooms it serves (Seg: the machinery segment while they are laid out).</summary>
public sealed class Funnel : IPyValue
{
    [JsonIgnore] public required string Id;
    public double X { get; set; }
    public double Y { get; set; }
    public double L { get; set; }
    public double W { get; set; }
    public long Pipes { get; set; }
    public double? Z0 { get; set; }
    public double Top { get; set; }
    [JsonIgnore] public long? Seg;
    [JsonIgnore] public List<string>? Serves;

    public object? ToPy() => JsonBridge.ToPy(this, ShipgenJson.Default.Funnel);
}

/// <summary>An AA mount: its type (Geometry.AA_CFG), where it stands, its rest bearing, its base above the main deck and
/// its drawing layer.</summary>
public sealed class AaMount : IPyValue
{
    [JsonIgnore] public required string Id;
    public required string Type { get; init; }
    public double X { get; init; }
    public double Y { get; init; }
    public long Dir { get; init; }
    [JsonIgnore] public double Base;
    public required string Layer { get; init; }

    public object? ToPy() => JsonBridge.ToPy(this, ShipgenJson.Default.AaMount);
}

/// <summary>A mast: where it stands, its yard, tripod legs, its top above the main deck (null: the layout's default)
/// and a merchant's cargo booms ([x, y] of each boom's head).</summary>
public sealed record Mast : IPyValue
{
    public double X { get; init; }
    public double? Y { get; init; }
    public double Yard { get; init; }
    public bool Tripod { get; init; }
    public double? Top { get; set; }
    public List<double[]>? Booms { get; init; }

    public object? ToPy() => JsonBridge.ToPy(this, ShipgenJson.Default.Mast);
}

/// <summary>A deck the layout adds over the main deck: a raised stretch of hull ("deck"), a flight deck, or a sponson,
/// with its outline and its base and top above the main deck.</summary>
public sealed record DeckPlate(string Id, string Kind, List<Pt> Points, double Base, double Top);

/// <summary>A compartment below the main deck: a room the subdivision gives cells to. HalfWidth about Y; Base and Top
/// above the main deck when it doesn't run from the inner bottom to the armour deck.</summary>
public sealed class Compartment
{
    public required string Id;
    public required string Kind;
    public double X0, X1, HalfWidth;
    public double? Y, Base, Top;
    /// <summary>A bunker's fuel.</summary>
    public string? Fuel;
    /// <summary>Split into one room per section, named by this format ("{}" the section's id).</summary>
    public string? PerSection;
    public double? Tonnes;
    /// <summary>A magazine's one mount, or its mounts.</summary>
    public string? Mount;
    public List<string>? Mounts;

    /// <summary>The fields beyond the room's box, as the subdivision reports them.</summary>
    public IEnumerable<(string Key, object? Value)> Extras()
    {
        if (Fuel != null)
            yield return ("fuel", Fuel);
        if (PerSection != null)
            yield return ("per_section", PerSection);
        if (Tonnes != null)
            yield return ("tonnes", Tonnes);
        if (Mount != null)
            yield return ("mount", Mount);
        if (Mounts != null)
            yield return ("mounts", Mounts.Cast<object?>().ToList());
    }
}

/// <summary>A zone ordnance is stowed in (Ordnance.Stow): X0..X1, HalfWidth about the centreline, its rooms aft to
/// forward, and whether it is one mount's own magazine.</summary>
public sealed record Zone(double X0, double X1, double HalfWidth, List<ZoneRoom> Rooms, bool Own = false);

/// <summary>A room in a zone: the mounts whose ammunition it holds and/or tonnes of something else at TPerM3.</summary>
public sealed record ZoneRoom(string Id, List<string>? Mounts = null, double Tonnes = 0, string? Kind = null, double? TPerM3 = null);

/// <summary>The casing over machinery taller than its space.</summary>
public sealed record Casing(string Id, double X0, double X1, double W, double Base, double Top, double ArmourMm);

/// <summary>The conning tower: a cylinder of radius R at X, Y, Top above the main deck.</summary>
public sealed record ConningTower(double X, double Y, double R, double Top);

/// <summary>A boat on deck (or a raft): L x W at X, Y, its top above the main deck when it isn't the default.</summary>
public sealed record Boat(double X, double Y, double L, double W, double? Top = null);

/// <summary>A merchant's cargo hatch.</summary>
public sealed record Hatch(double X, double Y, double L, double W);

/// <summary>A seaplane carrier's aircraft crane.</summary>
public sealed record Crane(double X, double Y, double R, long Dir, double Jib, double Top);

/// <summary>A deck fitting drawn as a coloured box (palette key Color), rounded by R.</summary>
public sealed record Fitting(double X, double Y, double L, double W, string Color, double? R = null);

/// <summary>A carrier's gun or AA sponson, as drawn.</summary>
public sealed record SponsonDrawing(double X, double Y, double L, double W);

/// <summary>The hull's planform: its length and beam, the bow and stern tapers (Hull fills in what they leave out),
/// and a planing hull's deck inset and plank spacing.</summary>
public sealed record HullSpec(double Length, double Beam, HullEnd Bow, HullEnd Stern, double? DeckInset = null,
    double? PlankSpacing = null);

/// <summary>One end's taper: how far it runs (fraction of L), its curve's power, its shape ("pointed", "round"), a
/// stern's transom (fraction of the beam) and a bow's flare.</summary>
public sealed record HullEnd : IPyValue
{
    public double? Taper { get; init; }
    public double? Power { get; init; }
    public string? Shape { get; init; }
    public double? Transom { get; init; }
    public double? Flare { get; init; }

    public object? ToPy() => JsonBridge.ToPy(this, ShipgenJson.Default.HullEnd);
}

/// <summary>A mount as the renderer draws it: its type, where it stands, its draw order and its rest bearing.</summary>
public sealed class SpecTurret
{
    public required string Id { get; init; }
    public required string Type { get; init; }
    public double X { get; init; }
    public double Y { get; init; }
    public long Z { get; init; }
    public double Rest { get; set; }
}

/// <summary>A raised stretch of hull as the renderer draws it.</summary>
public sealed record RaisedDeckSpec(double X0, double X1, long Levels);

/// <summary>What the renderer draws (lay.Spec): the hull, the turret types and mounts, the superstructure, funnels,
/// masts and AA, and each style's deck furniture.</summary>
public sealed class RenderSpec : IPyValue
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Class { get; init; }
    public double Length { get; init; }
    public double Beam { get; init; }
    public required HullEnd Bow { get; init; }
    public required HullEnd Stern { get; init; }
    /// <summary>The deck finish: "wood" or "steel".</summary>
    public required string Deck { get; init; }
    public required OrderedDictionary<string, TurretType> TurretTypes { get; init; }
    public required List<SpecTurret> Turrets { get; init; }
    public required List<Block> Superstructure { get; init; }
    public required List<Funnel> Funnels { get; init; }
    public required List<Mast> Masts { get; init; }
    public required List<AaMount> Aa { get; init; }
    public List<RaisedDeckSpec>? RaisedDecks { get; set; }
    public List<Boat>? Boats { get; set; }
    /// <summary>The x of the bollards at the bow and the stern.</summary>
    public List<double>? Bollards { get; set; }
    public double? ChainX { get; set; }
    public double? HawseBack { get; set; }
    public double? BreakwaterX { get; set; }
    public double? DeckInset { get; set; }
    public double? PlankSpacing { get; set; }
    public List<Fitting>? Fittings { get; set; }
    public List<Hatch>? Hatches { get; set; }
    public List<Crane>? Cranes { get; set; }
    public List<SponsonDrawing>? Sponsons { get; set; }
    public FlightDeckDrawing? FlightDeck { get; set; }

    public object? ToPy() => JsonBridge.ToPy(this, ShipgenJson.Default.RenderSpec);
}

/// <summary>A carrier's flight deck as drawn: its outline, the planks' box and pitch, the elevators, the arrester
/// wires (x1, y1, x2, y2), the painted marks and the deck number (null: none).</summary>
public sealed class FlightDeckDrawing
{
    public required List<Pt> Points { get; init; }
    public required FlightDeckPlanks Planks { get; init; }
    public List<SponsonDrawing> Elevators { get; init; } = [];
    public List<SponsonDrawing> EdgeElevators { get; init; } = [];
    public List<double[]> Wires { get; init; } = [];
    public List<DeckMark> Marks { get; init; } = [];
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)] public DeckNumber? Number { get; init; }
}

public sealed record FlightDeckPlanks(double X0, double X1, double Y0, double Y1, double Step);

/// <summary>A painted line on the flight deck: palette colour, width, and a dash pattern or opacity.</summary>
public sealed record DeckMark(double X1, double Y1, double X2, double Y2, string Color, double Width)
{
    public string? Dash { get; init; }
    public double? Opacity { get; init; }
}

/// <summary>The number painted on a flight deck.</summary>
public sealed record DeckNumber(double X, double Y, string Text, double Size);
