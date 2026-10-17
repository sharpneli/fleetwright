using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fleetwright.Shipgen;

// The small closed sets of the ship model as enums: a typo is a compile error, not a silent fall-through to a default.
// Each is written to JSON (and named in messages) as its members' names in snake_case (BoilerRoom: "boiler_room"), so
// the names are the members' own and listed nowhere else.

/// <summary>A compartment's (a room's) kind. The sim keeps these byte values in its tables: add new kinds at the end.</summary>
[JsonConverter(typeof(SnakeEnumConverter<RoomKind>))]
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
    /// <summary>Over a mount's own magazine, under its turret: the flash path's first space (protection-plan.md H1).</summary>
    Handling,
    /// <summary>A carrier's hangar bay: a compartment of the layout, never a room (the subdivision gives it no cells).</summary>
    Hangar,
}

/// <summary>A hitbox component's kind. Main, Secondary and Torpedo are the mounts by battery (the mount table); AA mounts
/// are their own catalogue, with no turret shape or bearing yet. The sim keeps these byte values in its tables: add new
/// kinds at the end.</summary>
[JsonConverter(typeof(SnakeEnumConverter<ComponentKind>))]
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

/// <summary>Where along the hull an armour strake or deck runs (the design's "ends" is a fore and an aft piece). The sim
/// keeps these byte values in its tables: add new extents at the end.</summary>
[JsonConverter(typeof(SnakeEnumConverter<Extent>))]
public enum Extent : byte
{
    Citadel,
    Fore,
    Aft,
    Full,
    Steering,
    /// <summary>A magazine box's crown or floor (protection-plan.md H5).</summary>
    Box,
}

/// <summary>How a hull's plates are joined (hull.construction.shell and .internals; docs/battle/shells-armour-damage.md
/// E0): what the blast's boundaries hold (riveted ×0.85), the belt's seep and the seams' leak read.</summary>
[JsonConverter(typeof(SnakeEnumConverter<Joint>))]
public enum Joint : byte
{
    Riveted,
    Welded,
}

/// <summary>How the ship's fittings stand shock (fittings.shock_grade; docs/damage 17 service §2.2): G0 unsecured, G1
/// secured, G2 shock-tested, G3 post-war grade-A gear. Written as its members' names ("G1"), as the research names
/// them.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ShockGrade>))]
public enum ShockGrade : byte
{
    G0,
    G1,
    G2,
    G3,
}

/// <summary>An enum's members by their snake_case names, both ways.</summary>
public static class SnakeEnum<T> where T : struct, Enum
{
    static readonly Dictionary<T, string> names = Enum.GetValues<T>().ToDictionary(v => v, v => Snake(v.ToString()));
    static readonly Dictionary<string, T> values = names.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.Ordinal);

    public static string Name(T v) => names[v];

    public static bool TryParse(string? s, out T v) => values.TryGetValue(s ?? "", out v);

    /// <summary>The member named s; unknown names throw.</summary>
    public static T Parse(string s) => TryParse(s, out var v) ? v : throw new ArgumentException(Unknown(s));

    /// <summary>The message for a name that isn't a member: unknown room kind "ballroom".</summary>
    public static string Unknown(string? s) => $"unknown {Snake(typeof(T).Name).Replace('_', ' ')} \"{s}\"";

    static string Snake(string pascal)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < pascal.Length; i++)
        {
            if (char.IsUpper(pascal[i]) && i > 0)
                sb.Append('_');
            sb.Append(char.ToLowerInvariant(pascal[i]));
        }
        return sb.ToString();
    }
}

public static class SnakeEnum
{
    /// <summary>The member's snake_case name, as the JSON writes it.</summary>
    public static string Name<T>(this T v) where T : struct, Enum => SnakeEnum<T>.Name(v);
}

/// <summary>An enum as its members' snake_case names.</summary>
public sealed class SnakeEnumConverter<T> : JsonConverter<T> where T : struct, Enum
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        SnakeEnum<T>.TryParse(reader.GetString(), out var v) ? v : throw new JsonException(SnakeEnum<T>.Unknown(reader.GetString()));

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) => writer.WriteStringValue(SnakeEnum<T>.Name(value));
}
