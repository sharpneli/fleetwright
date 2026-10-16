namespace Fleetwright.Sim.Ships;

/// <summary>A hitbox component's kind. Main, Secondary and Torpedo are the mounts by battery (the mount table); AA mounts
/// are their own catalogue, with no turret shape or bearing yet.</summary>
public enum ComponentKind : byte
{
    Main,
    Secondary,
    Torpedo,
    Aa,
    Barbette,
    Superstructure,
    ConningTower,
    Funnel,
    Uptake,
    Casing,
    Sponson,
    Mast,
    Shaft,
    ShaftAlley,
    Propeller,
    Rudder,
    TorpedoTube,
    FlightDeck,
    HangarBay,
}

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

public enum RoomKind : byte
{
    Stores,
    Accommodation,
    DoubleBottom,
    Magazine,
    Bunker,
    BoilerRoom,
    EngineRoom,
    Steering,
    Tds,
    TorpedoRoom,
    FuelTank,
    CargoTank,
    Hold,
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

/// <summary>The export's strings, read once into the enums. An unknown string is a compile error, so a new kind in
/// shipgen is noticed here instead of read as something else.</summary>
static class Kinds
{
    public static ComponentKind Component(string s) => s switch
    {
        "main" => ComponentKind.Main,
        "secondary" => ComponentKind.Secondary,
        "torpedo" => ComponentKind.Torpedo,
        "aa" => ComponentKind.Aa,
        "barbette" => ComponentKind.Barbette,
        "superstructure" => ComponentKind.Superstructure,
        "conning_tower" => ComponentKind.ConningTower,
        "funnel" => ComponentKind.Funnel,
        "uptake" => ComponentKind.Uptake,
        "casing" => ComponentKind.Casing,
        "sponson" => ComponentKind.Sponson,
        "mast" => ComponentKind.Mast,
        "shaft" => ComponentKind.Shaft,
        "shaft_alley" => ComponentKind.ShaftAlley,
        "propeller" => ComponentKind.Propeller,
        "rudder" => ComponentKind.Rudder,
        "torpedo_tube" => ComponentKind.TorpedoTube,
        "flight_deck" => ComponentKind.FlightDeck,
        "hangar_bay" => ComponentKind.HangarBay,
        _ => throw Unknown("component kind", s),
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

    public static RoomKind Room(string s) => s switch
    {
        "stores" => RoomKind.Stores,
        "accommodation" => RoomKind.Accommodation,
        "double_bottom" => RoomKind.DoubleBottom,
        "magazine" => RoomKind.Magazine,
        "bunker" => RoomKind.Bunker,
        "boiler_room" => RoomKind.BoilerRoom,
        "engine_room" => RoomKind.EngineRoom,
        "steering" => RoomKind.Steering,
        "tds" => RoomKind.Tds,
        "torpedo_room" => RoomKind.TorpedoRoom,
        "fuel_tank" => RoomKind.FuelTank,
        "cargo_tank" => RoomKind.CargoTank,
        "hold" => RoomKind.Hold,
        _ => throw Unknown("room kind", s),
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
        _ => throw Unknown("deck kind", s),
    };

    public static Band BandOf(string s) => s switch
    {
        "C" => Band.Centre,
        "P" => Band.Port,
        "S" => Band.Starboard,
        "CP" => Band.CentrePort,
        "CS" => Band.CentreStarboard,
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

    /// <summary>TRANSITIONAL (S0 to P3 of docs/battle/shells-armour-damage.md): a shell's outcome-table row.</summary>
    public static ShellQuality ShellQualityOf(string s) => s switch
    {
        "rn1916" => ShellQuality.Rn1916,
        "km1916" => ShellQuality.Km1916,
        "rn1918" => ShellQuality.Rn1918,
        "ww2" => ShellQuality.Ww2,
        "ijn_t91" => ShellQuality.IjnT91,
        _ => throw Unknown("shell outcome row", s),
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
