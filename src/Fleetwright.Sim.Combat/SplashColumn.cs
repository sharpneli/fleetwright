using Fleetwright.Sim.Core;

namespace Fleetwright.Sim.Combat;

/// <summary>A shell's splash column (research/vfx/shell-splashes.md; docs/visuals/splashes.md): its peak about 180
/// calibres high, half that at a flat fall (3°) and full above 15°; its shaft about 25 calibres wide; up in √(2H/g),
/// down the same parabola by that and a second more. Design estimates from scaling and period photographs (no measured
/// tables, the research says) [EST]. One model: the sim's masking (docs/battle/error-sources.md ES2) and the drawings
/// read it.</summary>
public static class SplashColumn
{
    public const double HeightPerM = 180, WidthPerM = 25, Gravity = 9.81;

    /// <summary>Its peak height, m, for a calibre (mm) falling at an angle (degrees).</summary>
    public static double PeakM(double calibreMm, double fallDeg) => HeightPerM * calibreMm / 1000 * (0.5 + 0.5 * Steep(fallDeg));

    /// <summary>The angle of fall's share of the full height, 0 at 3° to 1 at 15° (smoothly).</summary>
    public static double Steep(double fallDeg)
    {
        double t = Math.Clamp((fallDeg - 3) / 12, 0, 1);
        return t * t * (3 - 2 * t);
    }

    /// <summary>Its shaft's width, m.</summary>
    public static double WidthM(double calibreMm) => WidthPerM * calibreMm / 1000;

    /// <summary>Its rise to the peak, s.</summary>
    public static double RiseS(double peakM) => Math.Sqrt(2 * Math.Max(peakM, 0) / Gravity);

    /// <summary>How long its shaft stays dense, s: through its rise, then thinning as it falls by e^−2(τ−1) (the
    /// drawing's), half its water gone at τ = 1 + ln 2 / 2.</summary>
    public static double DenseS(double peakM) => RiseS(peakM) * (1 + Math.Log(2) / 2);

    /// <summary>How long it stands, s: up, and down by twice that and a second.</summary>
    public static double LifeS(double peakM) => 2 * RiseS(peakM) + 1;

    /// <summary>Its top at an age, m (0 before it starts and once it's down): up as h(1 − (1 − τ)²), down the same way
    /// over the rest of its life.</summary>
    public static double HeightAt(double peakM, double ageS)
    {
        double rise = RiseS(peakM), life = 2 * rise + 1;
        if (!(ageS >= 0) || ageS >= life || rise <= 0)
            return 0;
        double u = ageS < rise ? 1 - ageS / rise : (ageS - rise) / (life - rise);
        return peakM * (1 - u * u);
    }

    /// <summary>Past this a column has surely thinned (a 20" shell's dense shaft at full height, under 6 s), ms.</summary>
    public const long WithinMs = 10_000;

    /// <summary>Whether splash columns standing between an eye (a point on the sea under it, and its height, m) and a ship
    /// (her position and heading, length, beam and top over the water, m) hide her whole (docs/battle/error-sources.md
    /// ES2): each column in front of her whose dense shaft stands over the line from the eye to her top covers a band of
    /// her as he sees her, its width taken to her range; she's hidden only while those bands together cover her from end
    /// to end as she lies to him. A cut or a lay needs only an edge of her, a mast is enough (generous to the gunner: the
    /// user, 2026-11-03). The falls by where they came down, their peaks (0: none, a hit), calibres and when (ms); their
    /// size scaled (0: nothing hides her).</summary>
    public static bool Hides(Vec3d eye, double eyeM, Vec3d target, Vec3d forward, double lengthM, double beamM, double topM,
        ReadOnlySpan<Vec3d> at, ReadOnlySpan<float> peakM, ReadOnlySpan<float> calibreMm, ReadOnlySpan<long> ms, long now, double scale)
    {
        var d = target - eye;
        var up = target.Normalized();
        var u = d - up * Vec3d.Dot(d, up);
        double range = u.Length;
        if (range < 1 || scale <= 0)
            return false;
        u *= 1 / range;
        var w = Vec3d.Cross(up, u);
        double ca = Math.Abs(Vec3d.Dot(forward, u)), sa = Math.Sqrt(Math.Max(1 - ca * ca, 0));
        double half = 0.5 * (lengthM * sa + beamM * ca), near = range - 0.5 * (lengthM * ca + beamM * sa);
        Span<double> lo = stackalloc double[at.Length], hi = stackalloc double[at.Length];
        int n = 0;
        for (int k = 0; k < at.Length; k++)
        {
            if (peakM[k] <= 0 || now - ms[k] >= WithinMs)
                continue;
            var rel = at[k] - eye;
            double sk = Vec3d.Dot(rel, u);
            if (sk <= 1 || sk >= near)
                continue;   // behind the eye, or not in front of her
            double peak = scale * peakM[k], age = (now - ms[k]) / 1000.0;
            if (age >= DenseS(peak) || HeightAt(peak, age) < eyeM + (topM - eyeM) * sk / range)
                continue;   // thinned as it falls, or under the line to her top
            double c = Vec3d.Dot(rel, w) * range / sk, hw = 0.5 * scale * WidthM(calibreMm[k]) * range / sk;
            if (c + hw < -half || c - hw > half)
                continue;
            // kept in order of their left edges (a few at most)
            int j = n++;
            while (j > 0 && lo[j - 1] > c - hw)
            {
                (lo[j], hi[j]) = (lo[j - 1], hi[j - 1]);
                j--;
            }
            (lo[j], hi[j]) = (c - hw, c + hw);
        }
        double covered = -half;
        for (int j = 0; j < n && covered < half; j++)
        {
            if (lo[j] > covered)
                return false;   // a gap: her edge shows there
            covered = Math.Max(covered, hi[j]);
        }
        return n > 0 && covered >= half;
    }
}
