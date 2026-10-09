using System.Text.Json.Serialization;

namespace Fleetwright.Shipgen.Render;

/// <summary>A look's colours by palette key (Data/looks.jsonc documents them): a single colour, or a list for the
/// keys that are one ("levels", lightest last; "camo").</summary>
public sealed class Palette
{
    readonly Dictionary<string, Paint> paints;

    public Palette(IEnumerable<KeyValuePair<string, Paint>> paints) => this.paints = new(paints, StringComparer.Ordinal);

    public string this[string key] => paints[key].Colour;

    /// <summary>A list entry's colours.</summary>
    public IReadOnlyList<string> Colours(string key) => paints[key].Colours;

    public bool TryGet(string key, out string colour)
    {
        bool ok = paints.TryGetValue(key, out var p) && p.Count > 0;
        colour = ok ? p!.Colour : "";
        return ok;
    }

    /// <summary>The key's colour, or fallback when the palette has none.</summary>
    public string Or(string key, string fallback) => TryGet(key, out var c) ? c : fallback;

    public bool Has(string key) => paints.TryGetValue(key, out var p) && p.Count > 0;

    public IReadOnlyDictionary<string, Paint> Paints => paints;

    /// <summary>This palette with over's entries on top.</summary>
    public Palette Over(IEnumerable<KeyValuePair<string, Paint>>? over) =>
        over is null ? this : new(paints.Concat(over).GroupBy(kv => kv.Key).Select(g => g.Last()));
}

/// <summary>A look's drawing variations (Data/looks.jsonc, "shapes"): how it builds its blocks, funnels, masts and
/// turrets and what it paints on deck. Null: the drawing's default.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record Shapes
{
    /// <summary>The clutter kit (Clutter.KITS), by default the era's.</summary>
    public string? Clutter { get; init; }
    public double? ClutterDensity { get; init; }
    public double? RoofPlanks { get; init; }
    public bool? RoofRails { get; init; }

    // the hull as drawn
    public double? BowPower { get; init; }
    public double? BowFlare { get; init; }
    public double? Transom { get; init; }
    public double? Tumblehome { get; init; }

    // superstructure: "chamfer", "tower", "boxy", "soft", "bowfront"; block_round overrides the corner rounding
    public string? Blocks { get; init; }
    public double? TowerLevel { get; init; }
    public double[]? BlockRound { get; init; }

    // funnels: "box", "oval", "capped"; funnel_cap "pan" or "hat"
    public string? Funnel { get; init; }
    public string? FunnelCap { get; init; }
    public double? FunnelRound { get; init; }
    public double? FunnelSquareness { get; init; }
    public double? FunnelRake { get; init; }
    public double? FunnelBandW { get; init; }

    // masts: "pole", "fighting_top", "cage", "lattice"
    public string? Mast { get; init; }
    public double? Tripod { get; init; }
    public double? CageR { get; init; }
    public double? LatticeR { get; init; }
    public double? TopR { get; init; }
    public long? TopTiers { get; init; }

    // paint
    public bool? Dazzle { get; init; }
    public double? DazzleDecks { get; init; }
    public double? DazzleUpperworks { get; init; }
    public double? DeckLineOpacity { get; init; }
    public DeckStripes? DeckStripes { get; init; }
    public bool? Awnings { get; init; }
    public bool? HullNumber { get; init; }
    /// <summary>The hull number (the design's own).</summary>
    public string? Number { get; init; }
    /// <summary>Recognition bands across armoured turret roofs: palette keys or #rrggbb.</summary>
    public List<string>? TurretBands { get; init; }
}

/// <summary>Recognition stripes on the forecastle (and, Ends "both", the quarterdeck): N bands, chevrons (Slope) or
/// diagonals, in the given colours (palette keys or #rrggbb).</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DeckStripes(long? N, string? Pattern, double? Slope, List<string>? Colours, string? Ends);

/// <summary>sprite.json: how the game puts a ship's sprites together.</summary>
public sealed class SpriteMeta
{
    public string? Id { get; init; }
    public string? Name { get; init; }
    public double ScalePxPerM { get; init; }
    public required long[] SizePx { get; init; }
    public required double[] OriginPx { get; init; }
    public string Orientation { get; init; } = ShipSprites.ORIENTATION;
    public string[] LayerOrder { get; init; } = ["hull", "turrets (ascending z)"];
    public required SpriteLayers Layers { get; init; }
    public required OrderedDictionary<string, TurretSprite> TurretTypes { get; init; }
    public required SpriteMips Mips { get; init; }
    public required SpriteShadow Shadow { get; init; }
    public required List<SpriteMount> Mounts { get; init; }
    public required List<long[]> MipRects { get; init; }
}

public sealed record SpriteLayers(string Hull, string HullMips);

public sealed record TurretSprite(string File, long[] SizePx, double[] PivotPx, string Desc, double CalibreMm, string MipsFile,
    List<long[]> MipRects)
{
    public double? CalibreLength { get; init; }
}

public sealed record SpriteMips(long Levels, double[] ScalePxPerM, string Rule);

public sealed record SpriteShadow(string HeightMap, double HeightStepM, double DeckM, double MaxHeightM, string Note, string HeightMapMips);

/// <summary>A mount on the sprite: where its turret's pivot goes (metres and pixels), its draw order Z, its bearings and
/// its roof's height above the waterline.</summary>
public sealed record SpriteMount(string Id, string Kind, string Type, double[] PosM, double[] Px, double RestDeg, List<double[]> ArcsDeg,
    double[]? TraverseDeg, long Z, double TopM)
{
    public string? Mount { get; init; }
}

/// <summary>The renderer's JSON: snake_case keys, nulls left out. Source-generated (AOT).</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
    AllowTrailingCommas = true, Converters = [typeof(PaintConverter)])]
[JsonSerializable(typeof(Shapes))]
[JsonSerializable(typeof(Dictionary<string, Paint>))]
[JsonSerializable(typeof(SpriteMeta))]
public sealed partial class RenderJson : JsonSerializerContext;
