namespace Fleetwright.Sim.Ships;

/// <summary>A superstructure block's or sponson's role (None for the rest).</summary>
public enum ComponentRole : byte
{
    None,
    Bridge,
    Director,
    AftControl,
    Deckhouse,
    Casemate,
    Island,
    Hangar,
    Elevator,
    /// <summary>A control position's rangefinder in its hood (after control's, the conning tower's; fire-control.md F4b).</summary>
    Rangefinder,
}

/// <summary>How a component's plan is given: a polygon (Points), a circle (X, Y, R), a segment (P0 to P1, radius R), a
/// disc (a propeller: X, Y, Z, its diameter), or none (a mount, whose shape is its turret's Local body).</summary>
public enum ComponentShape : byte
{
    None,
    Polygon,
    Circle,
    Segment,
    Disc,
}

public enum Fuel : byte
{
    None,
    Oil,
    Coal,
    Petrol,
    Diesel,
}

public enum BulkheadKind : byte
{
    Main,
    Collision,
    Armoured,
    Wing,
    Tds,
    Centreline,
}

/// <summary>Which side a longitudinal bulkhead stands on: port, starboard or the centreline.</summary>
public enum Side : byte
{
    None,
    Port,
    Starboard,
    Centre,
}

public enum DeckKind : byte
{
    InnerBottom,
    Deck,
    Main,
    Raised,
    /// <summary>A deck over part of the hold only: the magazines' crowns (protection-plan.md H1).</summary>
    Platform,
}

/// <summary>A cell's band across the hull: the centre, a wing, or the centre and one wing merged.</summary>
public enum Band : byte
{
    Centre,
    Port,
    Starboard,
    CentrePort,
    CentreStarboard,
}

/// <summary>What joins two neighbouring cells: open (one room), a deck, or a bulkhead.</summary>
public enum JoinKind : byte
{
    Open,
    Deck,
    Bulkhead,
}

/// <summary>What a fire space is (docs/battle/fire.md): a watertight cell, a part (a superstructure block, a gunhouse), or
/// a section's strip of weather deck.</summary>
public enum FireSpaceKind : byte
{
    Cell,
    Part,
    Deck,
}

/// <summary>What a fire space is used for, as the outfit's loads go (damage/17 §2): nothing that burns from a shell (tanks,
/// voids, bunkers, magazines: those are the magazine model's), messdecks, cabins, offices, stores, machinery, or open
/// deck.</summary>
public enum FireUse : byte
{
    None,
    Quarters,
    Cabins,
    Offices,
    Stores,
    Machinery,
    Deck,
}

/// <summary>How a fire reaches the next space (docs/battle/fire.md B1; damage/17 §5): through the same room or the
/// superstructure's own ladders and doors (open), up, down or sideways through steel (a deck, a bulkhead: open too where
/// a burst opened it or the doors stand open), along the weather deck to the next section's, or out of a part's openings
/// onto its deck and back in.</summary>
public enum FireWay : byte
{
    Open,
    Up,
    Down,
    Side,
    Deck,
    Out,
}

/// <summary>The battery a director controls.</summary>
public enum Battery : byte
{
    None,
    Main,
    Secondary,
    Aa,
}

/// <summary>The design's look's era (Shipgen's Looks): what the weapon placeholders read until the designer gives the
/// facts.</summary>
public enum Era : byte
{
    Victorian,
    GreatWar,
    Treaty,
    Wwii,
    ColdWar,
}

/// <summary>A shaft's (and its propeller's) place across the stern, or a submerged tube's along the hull.</summary>
public enum Position : byte
{
    None,
    Centre,
    Inner,
    Outer,
    Wing,
    Bow,
    Beam,
    Stern,
}

/// <summary>A mast's kind (shipgen's MastPlan.Kinds): what its silhouette is (fire-control.md F4d).</summary>
public enum MastKind : byte
{
    Pole,
    Tripod,
    Cage,
    Lattice,
    Tower,
}

/// <summary>The export's strings, read once into the enums. An unknown string is a compile error, so a new kind in
/// shipgen is noticed here instead of read as something else.</summary>
static class Kinds
{
    public static MastKind Mast(string? s) => s switch
    {
        null or "pole" => MastKind.Pole,
        "tripod" => MastKind.Tripod,
        "cage" => MastKind.Cage,
        "lattice" => MastKind.Lattice,
        "tower" => MastKind.Tower,
        _ => throw Unknown("mast kind", s),
    };

    public static ComponentRole Role(string? s) => s switch
    {
        null => ComponentRole.None,
        "bridge" => ComponentRole.Bridge,
        "director" => ComponentRole.Director,
        "aft_control" => ComponentRole.AftControl,
        "deckhouse" => ComponentRole.Deckhouse,
        "casemate" => ComponentRole.Casemate,
        "island" => ComponentRole.Island,
        "hangar" => ComponentRole.Hangar,
        "elevator" => ComponentRole.Elevator,
        "rangefinder" => ComponentRole.Rangefinder,
        _ => throw Unknown("component role", s),
    };

    public static ComponentShape Shape(string? s) => s switch
    {
        null => ComponentShape.None,
        "polygon" => ComponentShape.Polygon,
        "circle" => ComponentShape.Circle,
        "segment" => ComponentShape.Segment,
        "disc" => ComponentShape.Disc,
        _ => throw Unknown("component shape", s),
    };

    public static Fuel FuelOf(string? s) => s switch
    {
        null => Fuel.None,
        "oil" => Fuel.Oil,
        "coal" => Fuel.Coal,
        "petrol" => Fuel.Petrol,
        "diesel" => Fuel.Diesel,
        _ => throw Unknown("fuel", s),
    };

    public static BulkheadKind Bulkhead(string s) => s switch
    {
        "main" => BulkheadKind.Main,
        "collision" => BulkheadKind.Collision,
        "armoured" => BulkheadKind.Armoured,
        "wing" => BulkheadKind.Wing,
        "tds" => BulkheadKind.Tds,
        "centreline" => BulkheadKind.Centreline,
        _ => throw Unknown("bulkhead kind", s),
    };

    public static Side SideOf(string? s) => s switch
    {
        null => Side.None,
        "P" => Side.Port,
        "S" => Side.Starboard,
        "C" => Side.Centre,
        _ => throw Unknown("bulkhead side", s),
    };

    public static DeckKind Deck(string s) => s switch
    {
        "inner_bottom" => DeckKind.InnerBottom,
        "deck" => DeckKind.Deck,
        "main" => DeckKind.Main,
        "raised" => DeckKind.Raised,
        "platform" => DeckKind.Platform,
        _ => throw Unknown("deck kind", s),
    };

    public static Band BandOf(string s) => s switch
    {
        "C" => Band.Centre,
        "P" => Band.Port,
        "S" => Band.Starboard,
        "CP" => Band.CentrePort,
        "CS" => Band.CentreStarboard,
        ['P', >= '1' and <= '9'] => Band.Port,   // a layered TDS's (protection-plan.md H3)
        ['S', >= '1' and <= '9'] => Band.Starboard,
        _ => throw Unknown("cell band", s),
    };

    public static Battery BatteryOf(string? s) => s switch
    {
        null => Battery.None,
        "main" => Battery.Main,
        "secondary" => Battery.Secondary,
        "aa" => Battery.Aa,
        _ => throw Unknown("battery", s),
    };

    public static ShellKind ShellKindOf(string s) => s switch
    {
        "ap" => ShellKind.Ap,
        "common" => ShellKind.Common,
        "he" => ShellKind.He,
        _ => throw Unknown("shell kind", s),
    };

    public static Fuze FuzeOf(string s) => s switch
    {
        "base" => Fuze.Base,
        "nose" => Fuze.Nose,
        _ => throw Unknown("fuze position", s),
    };

    public static Cap CapOf(string s) => s switch
    {
        "none" => Cap.None,
        "soft" => Cap.Soft,
        "hard" => Cap.Hard,
        "hard2" => Cap.Hard2,
        "break_away" => Cap.BreakAway,
        _ => throw Unknown("cap", s),
    };

    /// <summary>A fire-control computer's tracker (docs/battle/fire-control.md F2).</summary>
    /// <summary>An optics block's kind.</summary>
    public static InstrumentKind InstrumentOf(string? s) => s switch
    {
        "eye" => InstrumentKind.Eye,
        null or "coincidence" => InstrumentKind.Coincidence,
        "stereo" => InstrumentKind.Stereo,
        _ => throw Unknown("optics kind", s),
    };

    /// <summary>What a mast's top gives an instrument to stand on.</summary>
    public static InstrumentSupport SupportOf(MastKind k) => k switch
    {
        MastKind.Tripod => InstrumentSupport.Tripod,
        MastKind.Cage => InstrumentSupport.Cage,
        MastKind.Lattice => InstrumentSupport.Lattice,
        MastKind.Tower => InstrumentSupport.Tower,
        _ => InstrumentSupport.Pole,
    };

    public static TrackerKind TrackerOf(string? s) => s switch
    {
        "judgement" => TrackerKind.Judgement,
        "rate_clock" => TrackerKind.RateClock,
        "true_course" => TrackerKind.TrueCourse,
        "acceleration" => TrackerKind.Acceleration,
        "turn_rate" => TrackerKind.TurnRate,
        _ => throw Unknown("tracker", s ?? "(none)"),
    };

    /// <summary>A look's era; none is wwii, as Shipgen's own default.</summary>
    public static Era EraOf(string? s) => s switch
    {
        null or "wwii" => Era.Wwii,
        "victorian" => Era.Victorian,
        "great_war" => Era.GreatWar,
        "treaty" => Era.Treaty,
        "cold_war" => Era.ColdWar,
        _ => throw Unknown("era", s),
    };

    public static Position PositionOf(string? s) => s switch
    {
        null => Position.None,
        "centre" => Position.Centre,
        "inner" => Position.Inner,
        "outer" => Position.Outer,
        "wing" => Position.Wing,
        "bow" => Position.Bow,
        "beam" => Position.Beam,
        "stern" => Position.Stern,
        _ => throw Unknown("position", s),
    };

    static ShipCompileException Unknown(string what, string s) => new($"unknown {what} \"{s}\"");
}

/// <summary>A built ship the compiler can't take: an unknown kind, a link that doesn't resolve, a duplicate id.</summary>
public sealed class ShipCompileException(string message) : Exception(message);
