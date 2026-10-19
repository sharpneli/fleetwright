using Fleetwright.Shipgen;

namespace Fleetwright.Sim.Ships;

/// <summary>The boilers' kind (the plant's tech block, <see cref="Powerplant.BoilerKinds"/>).</summary>
public enum BoilerKind : byte
{
    None,
    Cylindrical,
    Locomotive,
    LargeTube,
    SmallTube,
}

/// <summary>The engines' kind (the plant's tech block, <see cref="Powerplant.EngineKinds"/>).</summary>
public enum EngineKind : byte
{
    Compound,
    Vte,
    DirectTurbine,
    GearedTurbine,
    Diesel,
    Petrol,
    GasTurbine,
}

/// <summary>What a medium slows a shell as (docs/battle/shell-contents.md; damage/09 §1.1): a liquid at the sea's
/// rate by density, a granular or distributed solid (coal, tube banks, blading, charges) at its own.</summary>
public enum MediumKind : byte
{
    None,
    Liquid,
    Granular,
}

/// <summary>How a struck solid meets the shell (damage/09 §1.2-1.3): a flat plate, at the path's obliquity to the room's
/// axes; a thin-walled cylinder, struck across its axis (sin θ uniform); a sphere or a drum's end (sin² θ uniform); a
/// bar, a thick round (a gun, a shaft, a crankshaft, a rudder stock, a stowed shell), whose steel chord and obliquity both
/// come from one draw of the impact offset.</summary>
public enum SolidShape : byte
{
    Flat,
    Cylinder,
    Sphere,
    Bar,
}

/// <summary>A struck solid as one plate: its thickness, mm (for a bar its outer diameter), drawn evenly from Lo to Hi; a
/// bar's bore as a share of its diameter; its material (a name in the material catalogue: MaterialTemplates) and shape.</summary>
public readonly record struct SolidRow(string Name, double LoMm, double HiMm, string Material, SolidShape Shape, double Bore = 0)
{
    public bool Some => HiMm > 0;
}

/// <summary>One kind of object in a room's contents (damage/09 §1.2): its share of the strikes; its skin; its interior,
/// a medium over a share of its chord (none: density 0); its core, met with a chance once through the skin.</summary>
public readonly record struct ContentObjectRow(double Share, SolidRow Skin, double InteriorRho = 0, double InteriorShare = 0,
    MediumKind Interior = MediumKind.None, double CoreChance = 0, SolidRow Core = default);

/// <summary>A room kind's contents (damage/09 §1.2): the band of the room's height they stand in (shares from its floor),
/// the share of the band's volume the solids take (the packing, 09's f), the mean chord of an object, m (Cauchy, 4V/S),
/// and what's struck.</summary>
public sealed record ContentsRow(string Name, double BandLo, double BandHi, double Packing, double ChordM, ContentObjectRow[] Mix);

/// <summary>A class's contents of one room, compiled (<see cref="ShipClass.ContentsOf"/>): the band as heights, the
/// mean free path to the next solid, λ = chord / packing (m), the object's chord, and its objects (a slice of
/// <see cref="ShipClass.ContentObjects"/>); none where <see cref="Some"/> is false.</summary>
public readonly record struct RoomContents(double ZLo, double ZHi, double LambdaM, double ChordM, Slice Mix, string Name)
{
    public bool Some => LambdaM > 0;
}

/// <summary>A compiled object (<see cref="ContentObjectRow"/> at this ship's size, its materials as the class's
/// indices): its share cumulated over its room's mix, so a draw finds it.</summary>
public readonly record struct ContentObject(double UpTo, string Name, SolidAt Skin, double InteriorRho, double InteriorShare,
    MediumKind Interior, double CoreChance, SolidAt Core);

/// <summary>A compiled solid: as <see cref="SolidRow"/>, its material the class's index.</summary>
public readonly record struct SolidAt(string Name, double LoMm, double HiMm, int Material, SolidShape Shape, double Bore)
{
    public bool Some => HiMm > 0;
}

/// <summary>What stands in a ship's spaces in a shell's path (docs/battle/shell-contents.md C1b), from
/// research/simulation/damage-research/09-contents-in-the-path.md §2 as written: rows by room kind and by the plant's
/// boiler and engine kinds, each at two sizes, a destroyer's and a large ship's. A ship gets her contents between the
/// two by her displacement (<see cref="SizeOf"/>): no switch between them, the objects grow with the ship. [EST] marks
/// what the research gave for one size only, the other estimated from it.</summary>
public static class Contents
{
    const string Mild = "mild steel", Forged = "high-tensile steel", CastIron = "cast iron";

    /// <summary>The displacements (t) of the small and large rows: a 1915 destroyer's and a dreadnought's. Between them a
    /// ship's size is ln(Δ/small) / ln(large/small), held to 0..1.</summary>
    public const double SmallT = 1_000, LargeT = 20_000;

    /// <summary>A ship's place between the small rows (0) and the large (1).</summary>
    public static double SizeOf(double displacementT) =>
        displacementT > 0 ? Math.Clamp(Math.Log(displacementT / SmallT) / Math.Log(LargeT / SmallT), 0, 1) : 1;

    // ------------------------------------------------------------------ boiler rooms (09 §2.2)

    static readonly ContentsRow CylindricalLarge = new("cylindrical boilers", 0, 0.85, 0.55, 2.6,
    [
        new(1, new("boiler shell", 45, 75, Mild, SolidShape.Cylinder), 1250, 0.75, MediumKind.Granular, 0.4,
            new("furnace", 25, 45, Mild, SolidShape.Cylinder)),
    ]);

    /// <summary>[EST] A torpedo boat's or small ship's fire-tube boiler, about 1.5 m across with 12-16 mm plate.</summary>
    static readonly ContentsRow CylindricalSmall = new("cylindrical boilers", 0, 0.85, 0.55, 1.7,
    [
        new(1, new("boiler shell", 22, 34, Mild, SolidShape.Cylinder), 1250, 0.75, MediumKind.Granular, 0.4,
            new("furnace", 15, 25, Mild, SolidShape.Cylinder)),
    ]);

    static readonly ContentsRow LargeTubeLarge = new("large-tube boilers", 0, 0.85, 0.50, 2.0,
    [
        new(1, new("casing and brick", 10, 10, Mild, SolidShape.Flat), 1100, 0.5, MediumKind.Granular, 0.15,
            new("steam collector", 20, 40, Mild, SolidShape.Cylinder)),
    ]);

    /// <summary>[EST] Scaled from the large ship's: smaller elements, thinner drums.</summary>
    static readonly ContentsRow LargeTubeSmall = new("large-tube boilers", 0, 0.85, 0.50, 1.5,
    [
        new(1, new("casing and brick", 8, 8, Mild, SolidShape.Flat), 1100, 0.5, MediumKind.Granular, 0.15,
            new("steam collector", 15, 30, Mild, SolidShape.Cylinder)),
    ]);

    /// <summary>Two halves for the drums: the steam drum met with 0.2 of strikes, a water drum 0.1.</summary>
    static readonly ContentsRow SmallTubeLarge = new("small-tube boilers", 0, 0.80, 0.55, 2.1,
    [
        new(0.5, new("casing", 8, 8, Mild, SolidShape.Flat), 1700, 0.4, MediumKind.Granular, 0.4,
            new("steam drum", 40, 70, Mild, SolidShape.Cylinder)),
        new(0.5, new("casing", 8, 8, Mild, SolidShape.Flat), 1700, 0.4, MediumKind.Granular, 0.2,
            new("water drum", 30, 40, Mild, SolidShape.Cylinder)),
    ]);

    /// <summary>A destroyer's: the research gives the steam drum only (0.2); her water drums [EST] are smaller still.</summary>
    static readonly ContentsRow SmallTubeSmall = new("small-tube boilers", 0, 0.80, 0.55, 1.7,
    [
        new(0.5, new("casing", 6, 6, Mild, SolidShape.Flat), 1700, 0.45, MediumKind.Granular, 0.4,
            new("steam drum", 25, 50, Mild, SolidShape.Cylinder)),
        new(0.5, new("casing", 6, 6, Mild, SolidShape.Flat), 1700, 0.45, MediumKind.Granular, 0,
            new("water drum", 20, 30, Mild, SolidShape.Cylinder)),
    ]);

    // ------------------------------------------------------------------ engine rooms (09 §2.3)

    static readonly ContentsRow VteLarge = new("reciprocating engines", 0, 0.90, 0.35, 1.6,
    [
        new(0.35, new("cylinder", 80, 150, CastIron, SolidShape.Cylinder), CoreChance: 0.3, Core: new("piston and rod", 120, 180, Forged, SolidShape.Cylinder)),
        new(0.25, new("columns, rods and valve gear", 20, 80, Forged, SolidShape.Cylinder)),
        new(0.20, new("crankshaft", 400, 500, Forged, SolidShape.Bar)),
        new(0.20, new("condenser", 12, 12, Mild, SolidShape.Flat), 1100, 0.85, MediumKind.Granular),
    ]);

    /// <summary>A destroyer's or torpedo boat's: crankshaft 150-220 mm (09); the rest [EST] scaled from the large.</summary>
    static readonly ContentsRow VteSmall = new("reciprocating engines", 0, 0.90, 0.35, 1.0,
    [
        new(0.35, new("cylinder", 35, 70, CastIron, SolidShape.Cylinder), CoreChance: 0.3, Core: new("piston and rod", 50, 90, Forged, SolidShape.Cylinder)),
        new(0.25, new("columns, rods and valve gear", 15, 40, Forged, SolidShape.Cylinder)),
        new(0.20, new("crankshaft", 150, 220, Forged, SolidShape.Bar)),
        new(0.20, new("condenser", 8, 8, Mild, SolidShape.Flat), 1100, 0.85, MediumKind.Granular),
    ]);

    /// <summary>Direct-drive turbines: cast-iron casings (most were, before about 1912; 09 §2.3).</summary>
    static readonly ContentsRow DirectLarge = new("turbines", 0, 0.55, 0.45, 1.4,
    [
        new(0.55, new("turbine casing", 60, 90, CastIron, SolidShape.Cylinder), 2000, 0.25, MediumKind.Granular, 0.8,
            new("turbine rotor", 100, 180, Forged, SolidShape.Cylinder)),
        new(0.35, new("condenser", 12, 12, Mild, SolidShape.Flat), 1100, 0.85, MediumKind.Granular),
        new(0.10, new("thrust block, shafting and pumps", 100, 200, Forged, SolidShape.Flat)),
    ]);

    static readonly ContentsRow DirectSmall = new("turbines", 0, 0.55, 0.45, 1.0,
    [
        new(0.55, new("turbine casing", 30, 50, CastIron, SolidShape.Cylinder), 2000, 0.25, MediumKind.Granular, 0.8,
            new("turbine rotor", 60, 100, Forged, SolidShape.Cylinder)),
        new(0.35, new("condenser", 8, 8, Mild, SolidShape.Flat), 1100, 0.85, MediumKind.Granular),
        new(0.10, new("thrust block, shafting and pumps", 60, 120, Forged, SolidShape.Flat)),
    ]);

    /// <summary>Geared turbines: cast-steel casings by then (mild steel's quality, 09 §2.3).</summary>
    static readonly ContentsRow GearedLarge = new("geared turbines", 0, 0.60, 0.45, 1.3,
    [
        new(0.45, new("turbine casing", 60, 90, Mild, SolidShape.Cylinder), 2000, 0.25, MediumKind.Granular, 0.8,
            new("turbine rotor", 100, 180, Forged, SolidShape.Cylinder)),
        new(0.15, new("gearing", 150, 400, Forged, SolidShape.Cylinder)),
        new(0.30, new("condenser", 12, 12, Mild, SolidShape.Flat), 1100, 0.85, MediumKind.Granular),
        new(0.10, new("thrust block, shafting and pumps", 100, 200, Forged, SolidShape.Flat)),
    ]);

    /// <summary>[EST] A destroyer's geared set (1916-18 on), from the direct turbines' small row and smaller gearing.</summary>
    static readonly ContentsRow GearedSmall = new("geared turbines", 0, 0.60, 0.45, 0.9,
    [
        new(0.45, new("turbine casing", 30, 50, Mild, SolidShape.Cylinder), 2000, 0.25, MediumKind.Granular, 0.8,
            new("turbine rotor", 60, 100, Forged, SolidShape.Cylinder)),
        new(0.15, new("gearing", 80, 200, Forged, SolidShape.Cylinder)),
        new(0.30, new("condenser", 8, 8, Mild, SolidShape.Flat), 1100, 0.85, MediumKind.Granular),
        new(0.10, new("thrust block, shafting and pumps", 60, 120, Forged, SolidShape.Flat)),
    ]);

    /// <summary>Auxiliary machinery (dynamos, hydraulic and pump rooms; 09 §2.3), and for now the engines a steam table
    /// doesn't cover: diesels, petrol engines, gas turbines.</summary>
    static readonly ContentsRow Auxiliary = new("machinery", 0, 0.50, 0.30, 0.8,
    [
        new(0.5, new("machinery", 20, 80, CastIron, SolidShape.Cylinder)),
        new(0.5, new("machinery", 20, 80, Mild, SolidShape.Flat)),
    ]);

    /// <summary>The small and large rows of a room of a kind on a ship with this plant (null: the room's empty).</summary>
    public static (ContentsRow Small, ContentsRow Large)? Of(RoomKind kind, BoilerKind boilers, EngineKind engines) => kind switch
    {
        RoomKind.BoilerRoom => boilers switch
        {
            BoilerKind.Cylindrical or BoilerKind.Locomotive => (CylindricalSmall, CylindricalLarge),
            BoilerKind.LargeTube => (LargeTubeSmall, LargeTubeLarge),
            BoilerKind.SmallTube => (SmallTubeSmall, SmallTubeLarge),
            _ => null,
        },
        RoomKind.EngineRoom => engines switch
        {
            EngineKind.Compound or EngineKind.Vte => (VteSmall, VteLarge),
            EngineKind.DirectTurbine => (DirectSmall, DirectLarge),
            EngineKind.GearedTurbine => (GearedSmall, GearedLarge),
            _ => (Auxiliary, Auxiliary),
        },
        _ => null,
    };

    /// <summary>A row between the small and the large at a size (0 small, 1 large), field by field. The two have the same
    /// objects in the same order; the names and materials are the large row's.</summary>
    public static ContentsRow Between(ContentsRow small, ContentsRow large, double size)
    {
        if (ReferenceEquals(small, large))
            return large;
        double L(double a, double b) => a + (b - a) * size;
        SolidRow S(SolidRow a, SolidRow b) => b with { LoMm = L(a.LoMm, b.LoMm), HiMm = L(a.HiMm, b.HiMm), Bore = L(a.Bore, b.Bore) };
        var mix = new ContentObjectRow[large.Mix.Length];
        for (int i = 0; i < mix.Length; i++)
        {
            var (a, b) = (small.Mix[i], large.Mix[i]);
            mix[i] = b with
            {
                Share = L(a.Share, b.Share), Skin = S(a.Skin, b.Skin), InteriorRho = L(a.InteriorRho, b.InteriorRho),
                InteriorShare = L(a.InteriorShare, b.InteriorShare), CoreChance = L(a.CoreChance, b.CoreChance), Core = S(a.Core, b.Core),
            };
        }
        return large with
        {
            BandLo = L(small.BandLo, large.BandLo), BandHi = L(small.BandHi, large.BandHi), Packing = L(small.Packing, large.Packing),
            ChordM = L(small.ChordM, large.ChordM), Mix = mix,
        };
    }

    public static BoilerKind BoilersOf(string? s) => s switch
    {
        "none" => BoilerKind.None,
        "cylindrical" => BoilerKind.Cylindrical,
        "locomotive" => BoilerKind.Locomotive,
        "large_tube" => BoilerKind.LargeTube,
        _ => BoilerKind.SmallTube,
    };

    public static EngineKind EnginesOf(string? s) => s switch
    {
        "compound" => EngineKind.Compound,
        "direct_turbine" => EngineKind.DirectTurbine,
        "geared_turbine" => EngineKind.GearedTurbine,
        "diesel" => EngineKind.Diesel,
        "petrol" => EngineKind.Petrol,
        "gas_turbine" => EngineKind.GasTurbine,
        _ => EngineKind.Vte,
    };

    // ------------------------------------------------------------------ the strike (damage/09 §1.3)

    /// <summary>A round bar's steel chord, mm, struck across its axis at offset b (0..R) from it: through the wall twice
    /// past its bore, 2(√(R²−b²) − √(r²−b²)), or its whole chord 2√(R²−b²) outside the bore.</summary>
    public static double BarChordMm(double odMm, double bore, double b)
    {
        double r2 = odMm * odMm / 4, b2 = b * b, i2 = bore * bore * r2;
        double outer = 2 * Math.Sqrt(Math.Max(r2 - b2, 0));
        return b2 < i2 ? outer - 2 * Math.Sqrt(i2 - b2) : outer;
    }

    /// <summary>A bar's mean steel chord, mm: (π/4)(OD² − ID²)/OD (damage/09 §1.3's table).</summary>
    public static double BarMeanChordMm(double odMm, double bore) => Math.PI / 4 * odMm * (1 - bore * bore);
}
