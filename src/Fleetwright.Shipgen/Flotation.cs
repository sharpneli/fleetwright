namespace Fleetwright.Shipgen;

/// <summary>How a ship floats with water in her (docs/battle/fighting.md 4.3b): linear from the full-load hydrostatics,
/// sinkage from TPC and trim from the moment over MCT. The one settling model: the damage model's flooding settles by it,
/// and the design's subdivision check (<see cref="Floodable"/>) floods its compartments by it.</summary>
public static class Flotation
{
    /// <summary>The sinkage water aboard gives, m.</summary>
    public static double SinkageM(double waterT, double tpcT) => waterT / (100 * Math.Max(tpcT, 1e-6));

    /// <summary>The trim a moment about the centre of flotation (t·m, bow down positive) gives, radians.</summary>
    public static double TrimRad(double momentTm, double mctTm, double length) =>
        Math.Atan(momentTm / Math.Max(mctTm, 1e-6) / 100 / Math.Max(length, 1));

    /// <summary>The sea's surface in the ship's frame (heights over the main deck) at an attitude: z = Z0 + DzDx·x + DzDy·y.
    /// waterline: the level waterline's height; lcf: the centre of flotation, which sinkage and trim are about.</summary>
    public static (double Z0, double DzDx, double DzDy) Sea(double waterline, double lcf, double sinkM, double trimRad, double heelRad)
    {
        double ct = Math.Cos(trimRad), st = Math.Sin(trimRad), cp = Math.Cos(heelRad), sp = Math.Sin(heelRad), tan = st / ct;
        return (waterline + sinkM / (ct * cp) - lcf * tan, tan, sp / (ct * cp));
    }
}
