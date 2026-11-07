using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>A detonation as its near field reads it: its size (W^⅓, W its TNT's worth in kg), how far water backs it (0 in air, 1 in
/// water) and how far it wrecks, m (<see cref="NearField.Charge"/>).</summary>
public readonly record struct NearCharge(double Size, double Tamp, double ReachM);

/// <summary>What a detonation does close to, whatever set it off (docs/battle/explosions.md EX4): one reach and one hole, from
/// how big it is, the plate it meets, how far off that plate is and how far water backs it. A shell's burst, a near miss and a
/// torpedo are the same rule at different places. A propellant's burn has no near field: its gas is all it has.
/// <list type="bullet">
/// <item><b>In air</b> the contact hole is <see cref="SimSettings.HoleRadiusK"/>·W^⅓·√(6/t) (damage/11 §3.1, unbiased against 13
/// measured holes), the casing's fragments and the shock together.</item>
/// <item><b>In water</b> it is <see cref="SimSettings.TampedHoleK"/>·W^⅓ whatever the plate, as what a tamped charge drives
/// into a hull is far past what its plate takes (the torpedo's 1.4·W^⅔ m²). Between them by <see cref="Tamp"/>: no switch.</item>
/// <item><b>Off the plate</b> the hole is what the reach's sphere cuts of it, and a thick plate lies within less of the reach:
/// a charge a little way off an armoured bulkhead doesn't hole it (Marblehead's 76 mm ruptured only in contact, 11 §3.2).</item>
/// </list></summary>
public static class NearField
{
    /// <summary>How far water backs a charge this deep in it, m: nothing at the surface, whole past
    /// <see cref="SimSettings.TampDepthK"/>·W^⅓, where the sea no longer vents it upwards.</summary>
    public static double Tamp(double depthM, double size, SimSettings s) =>
        size > 0 ? Math.Clamp(depthM / (s.TampDepthK * size), 0, 1) : 0;

    /// <summary>How far water backs a charge in a space with water in it: its depth under the space's level.</summary>
    public static double TampIn(ShipClass c, ShipCondition k, int cell, P3 at, double size, SimSettings s)
    {
        double water = k.WaterM3[cell];
        if (!(water > 0))
            return 0;
        ref readonly var x = ref c.Cells[cell];
        var b = x.Box;
        double plan = (b.X1 - b.X0) * (b.Y1 - b.Y0) * Math.Clamp(x.Permeability, 0.05, 1);
        return Tamp(b.Base + water / Math.Max(plan, 1e-3) - at.Z, size, s);
    }

    /// <summary>A charge of this size: force the share of it that goes off (a low-order burst, one in a plate).</summary>
    public static NearCharge Charge(double size, double force, double tamp, SimSettings s) =>
        new(size, tamp, Lerp(s.WreckRadiusK, s.TampedReachK, tamp) * size * Math.Sqrt(force));

    /// <summary>The contact hole's radius in a plate of mm, m.</summary>
    public static double HoleM(double size, double mm, double tamp, SimSettings s) =>
        Lerp(s.HoleRadiusK, s.TampedHoleK, tamp) * size * PlateK(mm, tamp);

    /// <summary>The hole's radius in a plate of mm this far from the charge, m: 0 where it's out of the plate's share of the reach.</summary>
    public static double HoleM(in NearCharge ch, double mm, double standoffM, SimSettings s)
    {
        double reach = ch.ReachM * Math.Min(PlateK(mm, ch.Tamp), 1);
        if (!(reach > standoffM))
            return 0;
        double share = standoffM / reach;
        return HoleM(ch.Size, mm, ch.Tamp, s) * Math.Sqrt(1 - share * share);
    }

    /// <summary>How much of a rectangle of plate a hole of radius r centred at (u, v) takes, m²: a square of its area clipped to it, so
    /// a plate split between spaces is holed once, however it's split.</summary>
    public static double Disc(double r, double u, double v, double u0, double u1, double v0, double v1) =>
        Math.PI / 4 * Math.Max(Math.Min(u + r, u1) - Math.Max(u - r, u0), 0) * Math.Max(Math.Min(v + r, v1) - Math.Max(v - r, v0), 0);

    /// <summary>The outside face of a cell nearest a point, as an index of <see cref="HullFaces"/>, or -1 if it has none.</summary>
    public static int NearestFace(ShipCondition k, int cell, P3 at)
    {
        var h = k.Faces;
        int best = -1;
        double nearest = double.MaxValue;
        for (int f = 0; f < 4; f++)
        {
            int x = cell * 4 + f;
            if (!(h.Area[x] > 0))
                continue;
            double d = f < 2 ? Math.Abs(at.Y - h.Point[x].Y) : Math.Abs(at.Z - h.Point[x].Z);
            if (d < nearest)
                (best, nearest) = (x, d);
        }
        return best;
    }

    /// <summary>A hole of its own in her outside, m²: the sea's way into the cell and the plate out of her section.</summary>
    public static void Punch(ShipClass c, ShipCondition k, int cell, double areaM2, P3 at, SimSettings s)
    {
        if (!(areaM2 > 0))
            return;
        k.Hole(cell, areaM2, at);
        int x = NearestFace(k, cell, at);
        if (x < 0)
            return;
        double area = k.Faces.Area[x];
        k.FaceTornM2[x] = Math.Min(k.FaceTornM2[x] + areaM2, area);
        double share = Blast.HoleShare(k.FaceTornM2[x], area);
        if (share > k.FaceLost[x])
            (k.FaceLost[x], k.GirderStale) = (share, true);
    }

    /// <summary>What a plate's thickness does to the hole: the root of 6 mm over it in air, nothing in water.</summary>
    static double PlateK(double mm, double tamp) => Math.Pow(6 / Math.Max(mm, 1), 0.5 * (1 - tamp));

    static double Lerp(double air, double water, double tamp) => air + (water - air) * tamp;
}
