using System.Runtime.CompilerServices;
using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>A detonation as its near field reads it: its size (W^⅓, W its TNT's worth in kg), how far water backs it (0 in air, 1 in
/// water), how far it wrecks, m (<see cref="NearField.Charge"/>), and the share of it that went off (a low-order burst's).</summary>
public readonly record struct NearCharge(double Size, double Tamp, double ReachM, double Force = 1);

/// <summary>What a detonation does close to, whatever set it off (docs/battle/explosions.md EX4, "The near field's hole"): one reach
/// and one hole, from how big it is, the plate it meets, how far off that plate is and how far water backs it. A shell's burst, a near
/// miss and a torpedo are the same rule at different places. A propellant's burn has no near field: its gas is all it has.
/// <para><b>The hole is where the plate tears:</b> the loading a plate takes falls with its distance from the charge, so what tears it
/// lies within a sphere about the charge, of the radius at which the loading meets what the plate takes (<see cref="TearM"/>). In
/// contact that's the hole; off the plate, the circle that sphere cuts of it: √(R² − d²), nothing past R. Three loadings, the largest
/// taking it:</para>
/// <list type="bullet">
/// <item><b>The casing's fragments and the shock together</b>, in contact in air: <see cref="SimSettings.HoleRadiusK"/>·W^⅓·√(6/t)
/// (damage/11 §3.1, unbiased against 13 measured shell holes). Off the plate the pieces' density falls as 1/R² from the burst, so the
/// radius where they still tear the plate between them is a sphere's.</item>
/// <item><b>The blast</b> in air: the reflected impulse (damage/11 §1.1's table, by Z = R/W^⅓) against Nurick's tearing threshold,
/// <see cref="SimSettings.TearKPaSMm"/> a mm of plate. Small charges' holes are their fragments'; a big bare charge's, the blast's.</item>
/// <item><b>The water's pulse</b>, under the sea: Cole's peak and decay (damage/15 §3.1), the momentum a plate of that mass takes from
/// it (Taylor's flat plate), times <see cref="SimSettings.WaterTakeK"/> for what a light plate takes after the water closes on it
/// (fitted to Seydlitz's 48 m² from ~200 kg), at most a rigid plate's twice, against the same threshold.</item>
/// </list>
/// Air and water by <see cref="Tamp"/>: no switch.</summary>
public static class NearField
{
    /// <summary>How far water backs a charge this deep in it, m: nothing at the surface, whole past
    /// <see cref="SimSettings.TampDepthK"/>·W^⅓, where the sea no longer vents it upwards.</summary>
    public static double Tamp(double depthM, double size, SimSettings s) =>
        size > 0 ? Math.Clamp(depthM / (s.TampDepthK * size), 0, 1) : 0;

    /// <summary>How far liquid backs a charge in a space with water or its own liquid in it (oil fuel, a TDS's water): its depth under
    /// the space's level.</summary>
    public static double TampIn(ShipClass c, ShipCondition k, int cell, P3 at, double size, SimSettings s)
    {
        ref readonly var x = ref c.Cells[cell];
        double water = k.WaterM3[cell] + x.VolumeM3 * x.Permeability * k.Liquid(cell);
        if (!(water > 0))
            return 0;
        var b = x.Box;
        double plan = (b.X1 - b.X0) * (b.Y1 - b.Y0) * Math.Clamp(x.Permeability, 0.05, 1);
        return Tamp(b.Base + water / Math.Max(plan, 1e-3) - at.Z, size, s);
    }

    /// <summary>A charge of this size: force the share of it that goes off (a low-order burst, one in a plate).</summary>
    public static NearCharge Charge(double size, double force, double tamp, SimSettings s) =>
        new(size, tamp, Lerp(s.WreckRadiusK, s.TampedReachK, tamp) * size * Math.Sqrt(force), force);

    /// <summary>How far a charge's near field is looked for among her plates, m: its wreck reach, or as far as it tears the thinnest
    /// plating she has, whichever is farther.</summary>
    public static double WalkM(in NearCharge ch, SimSettings s) => Math.Max(ch.ReachM, TearM(ch.Size, ch.Force, ThinMm, ch.Tamp, s));

    /// <summary>The thinnest plate a walk looks for, mm.</summary>
    const double ThinMm = 4;

    /// <summary>The contact hole's radius in a plate of mm, m: the whole charge's tear (<see cref="TearM"/>).</summary>
    public static double HoleM(double size, double mm, double tamp, SimSettings s) => TearM(size, 1, mm, tamp, s);

    /// <summary>The hole's radius in a plate of mm this far from the charge, m: the circle the charge's tear sphere cuts of it.</summary>
    public static double HoleM(in NearCharge ch, double mm, double standoffM, SimSettings s)
    {
        double r = TearM(ch.Size, ch.Force, mm, ch.Tamp, s);
        return r > standoffM ? Math.Sqrt(r * r - standoffM * standoffM) : 0;
    }

    /// <summary>The radius of the sphere about a charge (size W^⅓, force the share that went off) within which a plate of mm (steel)
    /// tears, m, as far as water backs it: in air the larger of its fragments' and its blast's, under water its pulse's.</summary>
    public static double TearM(double size, double force, double mm, double tamp, SimSettings s)
    {
        double w = size * size * size * Math.Max(force, 0);
        if (!(w > 0))
            return 0;
        mm = Math.Max(mm, 1);
        double air = tamp < 1 ? Math.Max(s.HoleRadiusK * Math.Cbrt(w) * Math.Sqrt(6 / mm), BlastTearM(w, mm, s)) : 0;
        return tamp > 0 ? Lerp(air, WaterTearM(w, mm, s), tamp) : air;
    }

    // ------------------------------------------------------------------ the blast in air

    /// <summary>damage/11 §1.1's reflected impulse over W^⅓, kPa·ms a kg^⅓, by Z = R/W^⅓ (m a kg^⅓) [SRC: Kingery-Bulmash, TNT, normal
    /// reflection]; log-log between, its end slopes past them.</summary>
    static readonly double[] ImpulseZ = [0.5, 1, 2, 5, 10], ImpulseK = [2370, 884, 364, 126, 59];

    /// <summary>How far the blast of w kg of TNT tears a plate of mm in air, m: where its reflected impulse falls to Nurick's
    /// threshold.</summary>
    static double BlastTearM(double w, double mm, SimSettings s)
    {
        double size = Math.Cbrt(w), need = Math.Log(1000 * s.TearKPaSMm * mm / size);   // kPa·ms a kg^⅓
        var z = ImpulseZ;
        var k = ImpulseK;
        int n = z.Length, a = 0;
        while (a < n - 2 && need < Math.Log(k[a + 1]))
            a++;
        double z0 = Math.Log(z[a]), z1 = Math.Log(z[a + 1]), k0 = Math.Log(k[a]), k1 = Math.Log(k[a + 1]);
        return Math.Exp(z0 + (need - k0) * (z1 - z0) / (k1 - k0)) * size;
    }

    // ------------------------------------------------------------------ the pulse in water

    const double Lb = 0.45359237, Ft = 0.3048, Psi = 6894.757, SteelKgM3 = 7850, WaterRhoC = 1025.0 * 1500;

    /// <summary>The momentum a plate of mm takes from w kg's pulse at R m in water, kPa·s: Cole's peak 21,600·(W^⅓/R)^1.13 psi and
    /// decay 6.0·10⁻⁵·W^⅓·(R/W^⅓)^0.18 s (lb, ft; damage/15 §3.1), Taylor's flat plate's share 2ψ^(ψ/(1−ψ)) of its free-field
    /// impulse (ψ = ρc·θ/m), times <see cref="SimSettings.WaterTakeK"/>, at most a rigid plate's 2.</summary>
    public static double WaterImpulseKPaS(double w, double mm, double r, SimSettings s)
    {
        double cube = Math.Cbrt(w / Lb), rFt = Math.Max(r, 1e-3) / Ft;
        double peak = 21_600 * Math.Pow(cube / rFt, 1.13) * Psi, theta = 6.0e-5 * cube * Math.Pow(rFt / cube, 0.18);
        double psi = WaterRhoC * theta / (SteelKgM3 * mm / 1000);
        double share = Math.Abs(psi - 1) < 1e-6 ? 2 / Math.E : 2 * Math.Pow(psi, psi / (1 - psi));
        return peak * theta * Math.Min(2, s.WaterTakeK * share) / 1000;
    }

    /// <summary>How far w kg's pulse tears a plate of mm in water, m: where the momentum it takes falls to Nurick's threshold (by
    /// halving in log R; the last few asked kept per thread).</summary>
    static double WaterTearM(double w, double mm, SimSettings s)
    {
        ref var c = ref cache;
        for (int i = 0; i < Cached; i++)
            if (c.W[i] == w && c.Mm[i] == mm && c.K[i] == s.WaterTakeK && c.T[i] == s.TearKPaSMm)
                return c.R[i];
        double need = s.TearKPaSMm * mm, lo = 1e-3, hi = 300, r = 0;
        if (WaterImpulseKPaS(w, mm, lo, s) >= need)
        {
            for (int i = 0; i < 48; i++)
            {
                double mid = Math.Sqrt(lo * hi);
                if (WaterImpulseKPaS(w, mm, mid, s) >= need)
                    lo = mid;
                else
                    hi = mid;
            }
            r = lo;
        }
        int at = c.Next;
        (c.W[at], c.Mm[at], c.K[at], c.T[at], c.R[at], c.Next) = (w, mm, s.WaterTakeK, s.TearKPaSMm, r, (at + 1) % Cached);
        return r;
    }

    const int Cached = 4;

    [InlineArray(Cached)]
    struct Four
    {
        double e;
    }

    struct TearCache
    {
        public Four W, Mm, K, T, R;
        public int Next;
    }

    [ThreadStatic]
    static TearCache cache;

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
        k.Torn(x, k.TearMs);
        double share = Blast.HoleShare(k.FaceTornM2[x], area);
        if (share > k.FaceLost[x])
            (k.FaceLost[x], k.GirderStale) = (share, true);
    }

    /// <summary>What a plate's thickness does to the reach behind it: the root of 6 mm over it in air, nothing in water.</summary>
    static double PlateK(double mm, double tamp) => Math.Pow(6 / Math.Max(mm, 1), 0.5 * (1 - tamp));

    /// <summary>The share of a contact burst's reach in air that a plate of mm lets past it: all of it through thin plating, √(6/mm)
    /// through armour.</summary>
    public static double PlateShare(double mm) => Math.Min(PlateK(mm, 0), 1);

    /// <summary>The share of a near field's reach a plate of mm lets past it, as tamped as the charge is: what it wrecks behind a plate
    /// it holed lies within that share of its reach (all of it through thin plating, and from a charge the sea backs whole).</summary>
    public static double Share(double mm, double tamp) => Math.Min(PlateK(mm, tamp), 1);

    static double Lerp(double air, double water, double tamp) => air + (water - air) * tamp;
}
