using Fleetwright.Sim;
using Fleetwright.Sim.Combat;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Map;

/// <summary>A ship as her damage leaves her, for the world view's effects (docs/visuals/destruction.md): the attitude of the
/// piece of her a point is on (broken in two, each piece its own: <see cref="Attitude.Piece"/>, the one the hit queries read),
/// and whether a point of her is still there and over the sea.</summary>
public static class Wreck
{
    /// <summary>Her attitude where ship x is, at a moment: the bow's or the stern's piece once she's broken, else her own.</summary>
    public static Attitude At(ShipClass c, ShipsView v, int k, double x, long ms)
    {
        double at = v.BreakX[k];
        if (!double.IsNaN(at) && v.Loss[k] != LossKind.None)
            return Attitude.Piece(c, v.Attitude[k], at, x >= at, v.LossMs[k], v.GoneMs[k], ms);
        return Attitude.At(c, v.Attitude[k], v.Loss[k], v.LossMs[k], v.GoneMs[k], ms);
    }

    /// <summary>Whether a point of her (ship frame, the class's heights) is still part of her and over the sea: not past an end
    /// that came off, and over the water at her attitude there.</summary>
    public static bool Shows(ShipClass c, ShipsView v, int k, P3 p, long ms)
    {
        if (p.X > v.CutBowX[k] || p.X < v.CutSternX[k])
            return false;
        return p.Z > HitQuery.WaterOf(c, At(c, v, k, p.X, ms)).At(p.X, p.Y);
    }
}
