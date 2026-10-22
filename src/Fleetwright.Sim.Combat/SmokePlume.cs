using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Combat;

/// <summary>A ship's smoke as something seen from afar (docs/battle/smoke.md Z6): what of it shows as a mass against the
/// sky at a view, how high, how dark; where it lies and which way its trail runs.
/// <list type="bullet">
/// <item><b>What shows</b> is where the smoke's depth through it, as the eye looks, is over
/// <see cref="SimSettings.SightSmokeTau"/>: a Gaussian seen through shows a region of 2πσ_yσ_z ln(τ_p/τ_s) (its peak
/// depth τ_p = A / (2πσ_yσ_z), σ_y its spread across the line of sight), its mean depth there (τ_p − τ_s) / ln(τ_p/τ_s).
/// Thin smoke, however wide, shows nothing: its contrast is a fraction of the sky's.</item>
/// <item><b>Two ways of adding it up:</b> puff by puff (each seen at the view against its own long way, her trail's way
/// as it's laid; their regions add: right side on, where they stand side by side), and the whole of it as one Gaussian
/// over the trail's spread (right end on, where they stand behind each other). The puffs' sum is taken at most at the
/// sum side on, and the whole's stack where it shows more: looking along a trail shows its stack, and an oblique view no
/// more than side on (a line crossing it obliquely goes deeper through it; not counted).</item>
/// <item><b>How dark:</b> each tint's own contrast against the horizon sky (<see cref="SimSettings.SightSmokeContrast"/>:
/// coal's and fires' black, oil's brown, powder's and chemicals' white) times 1 − e^(−τ̄).</item>
/// <item><b>How high:</b> the regions' heights as one Gaussian (their mean and spread), so the horizon hides the share
/// under it.</item>
/// </list>
/// Rough by design (docs/battle/smoke.md "The principles": physical where it decides): puffs side by side along a trail
/// overlap and show darker together than each alone; none of that is counted.</summary>
public readonly record struct SmokePlume(Vec3d Centre, Vec3d Axis, double AlongM, double AcrossM, double AreaM2, double Z, double SigmaZ,
    double Contrast, double PuffZ, double PuffSigmaZ, PlumeViews Puffs, double ShowsTau)
{
    /// <summary>The views the puffs' sum is kept at: the sine of the line of sight against a puff's long way.</summary>
    public static readonly double[] ViewSines = [1, 0.7071, 0.342, 0];

    /// <summary>Whether anything of it shows.</summary>
    public bool Shows => AreaM2 > 0 && (Puffs.Shows || Whole(0).AreaM2 > 0);

    /// <summary>Its top as it shows, m: two σ over its puffs' mean height side on, or over the whole's end on.</summary>
    public double TopM
    {
        get
        {
            var w = Whole(0);
            return Math.Max(Puffs.Shows ? PuffZ + 2 * PuffSigmaZ : 0, w.AreaM2 > 0 ? Z + 2 * Math.Sqrt(w.VarZ) : 0);
        }
    }

    /// <summary>The whole of it as one Gaussian at a view (its region over the showing depth).</summary>
    (double AreaM2, double Tau, double VarZ) Whole(double sinView) =>
        PlumeSum.Region(AreaM2, AcrossView(sinView), SigmaZ, ShowsTau);

    /// <summary>What shows over a height (the horizon's), m², and its contrast against the sky, seen at a view whose sine
    /// against its trail is <paramref name="sinView"/> (1 side on, 0 end on).</summary>
    public (double AreaM2, double Shade) Showing(double hiddenM, double sinView)
    {
        double s = Math.Clamp(Math.Abs(sinView), 0, 1);
        var (puffs, puffShade) = Puffs.At(s);
        puffs = Math.Min(puffs, Puffs.Side);
        var (whole, tau, varZ) = Whole(s);
        if (whole > puffs)
            return (whole * Above(Z, Math.Sqrt(varZ), hiddenM), Contrast * (1 - Math.Exp(-tau)));
        return puffs > 0 ? (puffs * Above(PuffZ, PuffSigmaZ, hiddenM), puffShade) : (0, 0);
    }

    /// <summary>Its spread across a line of sight at that view, m (for the bearing it's taken on).</summary>
    public double AcrossView(double sinView) => Math.Sqrt(AlongM * AlongM * sinView * sinView + AcrossM * AcrossM * (1 - sinView * sinView));

    static double Above(double z, double sigma, double h) => 0.5 * (1 + SmokeModel.Erf((z - h) / (Math.Sqrt(2) * Math.Max(sigma, 1e-3))));
}

/// <summary>The puffs' regions summed at the views of <see cref="SmokePlume.ViewSines"/>: area, m², and contrast.</summary>
public readonly record struct PlumeViews(double A0, double A1, double A2, double A3, double S0, double S1, double S2, double S3)
{
    public bool Shows => A0 > 0 || A1 > 0 || A2 > 0 || A3 > 0;

    /// <summary>Side on's.</summary>
    public double Side => A0;

    /// <summary>At a view's sine: between the two kept either side, linearly.</summary>
    public (double AreaM2, double Shade) At(double sinView)
    {
        var v = SmokePlume.ViewSines;
        int k = sinView >= v[1] ? 0 : sinView >= v[2] ? 1 : 2;
        double f = (v[k] - sinView) / (v[k] - v[k + 1]);
        double a = Lerp(Area(k), Area(k + 1), f);
        double s = a > 0 ? Lerp(Area(k) * Shade(k), Area(k + 1) * Shade(k + 1), f) / a : 0;
        return (a, s);
    }

    double Area(int k) => k switch { 0 => A0, 1 => A1, 2 => A2, _ => A3 };
    double Shade(int k) => k switch { 0 => S0, 1 => S1, 2 => S2, _ => S3 };
    static double Lerp(double a, double b, double f) => a + (b - a) * f;
}

/// <summary>A plume summed puff by puff (<see cref="SmokePlume"/>): <see cref="Add"/> each of a ship's puffs as it is
/// now, then <see cref="Plume"/>. Moments about the first puff's centre (no cancellation at the Earth's radius).</summary>
public struct PlumeSum
{
    Vec3d origin;
    double a, cx, cy, cz, cxx, cxy, cxz, cyy, cyz, czz, h2, z, zz, c0;
    double puffZ, puffZz, a0, a1, a2, a3, s0, s1, s2, s3;

    /// <summary>Puffs added.</summary>
    public int Count { get; private set; }

    /// <summary>A puff as it is now, its tint's contrast against the sky, and the depth over which smoke shows as a mass
    /// (<see cref="SimSettings.SightSmokeTau"/>).</summary>
    public void Add(in PuffState p, double contrast, double showsTau)
    {
        if (!(p.Area > 0))
            return;
        if (Count++ == 0)
            origin = p.Centre;
        var d = p.Centre - origin;
        double w = p.Area;
        a += w;
        (cx, cy, cz) = (cx + w * d.X, cy + w * d.Y, cz + w * d.Z);
        (cxx, cxy, cxz, cyy, cyz, czz) = (cxx + w * d.X * d.X, cxy + w * d.X * d.Y, cxz + w * d.X * d.Z, cyy + w * d.Y * d.Y, cyz + w * d.Y * d.Z,
            czz + w * d.Z * d.Z);
        h2 += w * p.SigmaH2;
        z += w * p.Z;
        zz += w * (p.Z * p.Z + p.SigmaZ2);
        c0 += w * contrast;
        // its region at each view against its long way; its heights by side on's
        double sz = Math.Sqrt(p.SigmaZ2), al2 = Math.Max(p.Along2, 0);
        var v = SmokePlume.ViewSines;
        for (int k = 0; k < 4; k++)
        {
            var (area, tau, varZ) = Region(p.Area, Math.Sqrt(p.SigmaH2 + al2 * v[k] * v[k]), sz, showsTau);
            if (!(area > 0))
                continue;
            double shade = area * contrast * (1 - Math.Exp(-tau));
            switch (k)
            {
                case 0:
                    (a0, s0) = (a0 + area, s0 + shade);
                    puffZ += area * p.Z;
                    puffZz += area * (p.Z * p.Z + varZ);
                    break;
                case 1:
                    (a1, s1) = (a1 + area, s1 + shade);
                    break;
                case 2:
                    (a2, s2) = (a2 + area, s2 + shade);
                    break;
                default:
                    (a3, s3) = (a3 + area, s3 + shade);
                    break;
            }
        }
    }

    /// <summary>The plume summed; none when nothing was added.</summary>
    public readonly SmokePlume Plume(double showsTau)
    {
        if (!(a > 0))
            return default;
        var mean = new Vec3d(cx, cy, cz) / a;
        var centre = (origin + mean).Normalized() * Earth.Radius;
        var east = Earth.East(centre);
        var north = Earth.North(centre);
        // the centres' covariance on the sea there: the trail's long way and its spread along and across it
        double ee = Cov(east, east, mean), en = Cov(east, north, mean), nn = Cov(north, north, mean);
        double half = (ee + nn) / 2, root = Math.Sqrt(Math.Max((ee - nn) * (ee - nn) / 4 + en * en, 0));
        double l1 = Math.Max(half + root, 0), l2 = Math.Max(half - root, 0), th = 0.5 * Math.Atan2(2 * en, ee - nn);
        var axis = east * Math.Cos(th) + north * Math.Sin(th);
        double meanH2 = h2 / a, zm = z / a, pz = a0 > 0 ? puffZ / a0 : 0;
        return new SmokePlume(centre, axis, Math.Sqrt(l1 + meanH2), Math.Sqrt(l2 + meanH2), a, zm, Math.Sqrt(Math.Max(zz / a - zm * zm, 1e-6)),
            c0 / a, pz, a0 > 0 ? Math.Sqrt(Math.Max(puffZz / a0 - pz * pz, 1e-6)) : 0,
            new PlumeViews(a0, a1, a2, a3, a0 > 0 ? s0 / a0 : 0, a1 > 0 ? s1 / a1 : 0, a2 > 0 ? s2 / a2 : 0, a3 > 0 ? s3 / a3 : 0), showsTau);
    }

    /// <summary>The centres' covariance along two ways, m² (about their mean offset).</summary>
    readonly double Cov(Vec3d u, Vec3d v, Vec3d mean) =>
        (u.X * (cxx * v.X + cxy * v.Y + cxz * v.Z) + u.Y * (cxy * v.X + cyy * v.Y + cyz * v.Z) + u.Z * (cxz * v.X + cyz * v.Y + czz * v.Z)) / a -
        Vec3d.Dot(u, mean) * Vec3d.Dot(v, mean);

    /// <summary>Where a Gaussian of area <paramref name="area"/> seen through (σ across the view and up) is deeper than
    /// <paramref name="showsTau"/>: that region's area, m², its mean depth, and its heights' variance, m².</summary>
    public static (double AreaM2, double Tau, double VarZ) Region(double area, double sigmaY, double sigmaZ, double showsTau)
    {
        double peak = area / (2 * Math.PI * Math.Max(sigmaY * sigmaZ, 1e-6));
        if (!(peak > showsTau))
            return (0, 0, 0);
        double ln = Math.Log(peak / showsTau);
        return (2 * Math.PI * sigmaY * sigmaZ * ln, (peak - showsTau) / ln, sigmaZ * sigmaZ * ln / 2);
    }
}
