using Fleetwright.Sim.Core;
using Fleetwright.Sim.Ships;

namespace Fleetwright.Sim.Combat;

/// <summary>How a gun is laid to put its shell on a point: the shell's flight (the plane's heading at the muzzle, the
/// elevation and speed it leaves with, the ship's own motion in, as <see cref="BallisticTable.Position"/> takes them) and
/// the gun's own lay (its elevation, and its horizontal direction in the world), with the time of flight.</summary>
public readonly record struct Lay(Vec3d Forward, double ElevationDeg, double Speed, double GunElevationDeg, Vec3d GunHorizontal, double TofS);

/// <summary>The fire-control chain's pure models (docs/battle/fighting.md 4.2b, a port of
/// research/simulation/fire_control_ref.py <c>engage()</c> cut to its spine): what a reading's error is, how a salvo
/// spreads, how a gun is laid for a point with its ship's motion, and how a spotter's judgement corrects the next
/// salvo. The gunnery system (the world's) runs them in time.</summary>
public static class FireControl
{
    public const double Mil = 1e-3;
    const double Arcsec = Math.PI / 648_000;

    /// <summary>One unit of a rangefinder's error at a range, metres: δ·R²/(B_eff·M), the optics' acuity over its base
    /// times magnification, a base past the optics' rigid one delivering as the flex power of its length
    /// (fire-control-designer.md §3.1, NDRC 1941; Sensor.unit_error). The one law: fire control's readings and the
    /// sighting's fixes both take it.</summary>
    public static double RangefinderUnitM(double baseM, in OpticsRow o, double rangeM)
    {
        double b = baseM > o.RigidBaseM && o.RigidBaseM > 0 ? o.RigidBaseM * Math.Pow(baseM / o.RigidBaseM, o.FlexExp) : baseM;
        return o.DeltaArcsec * Arcsec * rangeM * rangeM / Math.Max(b * o.Magnification, 1e-6);
    }

    /// <summary>A salvo of n guns' own dispersion, 1 sigma in range and across, metres: the pattern (its 100% spread,
    /// % of the range) is ratio(n)·D, D = 0.798σ (USNI 1917), times 0.85 for the gun-to-gun scatter in quoted
    /// patterns (Gun.sigma_D).</summary>
    public static (double Range, double Across) Dispersion(double patternPct, double rangeM, int guns, double deflectionRatio)
    {
        double ratio = Interp(guns, PatternGuns, PatternRatio);
        double sr = patternPct / 100 * rangeM / ratio / 0.798 * 0.85;
        return (sr, deflectionRatio * sr);
    }

    static readonly double[] PatternGuns = [1, 2, 3, 4, 6, 8, 9, 10, 12], PatternRatio = [1.6, 1.9, 2.43, 2.74, 3.47, 3.85, 4.0, 4.13, 4.34];

    static double Interp(double x, double[] xs, double[] ys)
    {
        if (x <= xs[0])
            return ys[0];
        for (int i = 1; i < xs.Length; i++)
            if (x <= xs[i])
                return ys[i - 1] + (ys[i] - ys[i - 1]) * (x - xs[i - 1]) / (xs[i] - xs[i - 1]);
        return ys[^1];
    }

    /// <summary>How a gun at a muzzle on a ship moving at a velocity is laid so its shell comes down on a point at sea
    /// level: the shell leaves with the gun's velocity and the ship's, so the gun is trained off the line by the ship's
    /// motion across it, and the elevation is the one whose flight, at the speed and angle the two give, falls at the
    /// point's range (the range table's, by the secant). False beyond the gun's reach. The Earth's turning is left out,
    /// as the era's computers left it: the spot takes it off with the rest of the bias.</summary>
    /// <param name="guessDeg">An elevation near the answer (last second's), or NaN.</param>
    public static bool Solve(BallisticTable t, Vec3d muzzle, Vec3d shipVelocity, Vec3d aim, out Lay lay, double guessDeg = double.NaN)
    {
        lay = default;
        var up = muzzle.Normalized();
        double h0 = muzzle.Length - Earth.Radius;
        var toward = aim - up * Vec3d.Dot(aim, up);
        if (toward.Length < 1e-6)
            return false;
        var f = toward.Normalized();
        var c = Vec3d.Cross(f, up);   // to the right of the line
        double v0 = t.Key.MuzzleVelocityMs, vf = Vec3d.Dot(shipVelocity, f), vc = Vec3d.Dot(shipVelocity, c), vu = Vec3d.Dot(shipVelocity, up);
        double k = Math.Sqrt(Math.Max(v0 * v0 - vc * vc, 1));
        double d = Earth.Distance(muzzle, aim);
        double e0 = t.ElevationFor(d, h0, 1, guessDeg);
        if (double.IsNaN(e0))
            return false;
        // the gun's angle in the plane of fire, θ: the shell leaves at vh = vf + k cos θ along, vu + k sin θ up
        double Miss(double thetaDeg, out double e, out double speed, out double tof)
        {
            double th = double.DegreesToRadians(thetaDeg), vh = vf + k * Math.Cos(th), vz = vu + k * Math.Sin(th);
            e = double.RadiansToDegrees(Math.Atan2(vz, vh));
            speed = Math.Sqrt(vh * vh + vz * vz) / v0;
            var fall = t.Fall(e, h0, speed);
            tof = fall.Tof;
            return fall.Range - d;
        }
        double a = e0, ma = Miss(a, out double ea, out double sa, out double ta);
        double b = e0 + 0.05, mb = Miss(b, out double eb, out double sb, out double tb);
        for (int i = 0; i < 8 && Math.Abs(mb) > 0.01 && mb != ma; i++)
        {
            double next = b - mb * (b - a) / (mb - ma);
            (a, ma) = (b, mb);
            b = next;
            mb = Miss(b, out eb, out sb, out tb);
        }
        if (!(Math.Abs(mb) < 1) || b > t.Key.MaxElevationDeg + 1)
            return false;
        double thb = double.DegreesToRadians(b);
        var gunHorizontal = (f * (k * Math.Cos(thb)) - c * vc).Normalized();
        lay = new Lay(f, eb, sb, double.RadiansToDegrees(Math.Asin(Math.Clamp(k * Math.Sin(thb) / v0, -1, 1))), gunHorizontal, tb);
        return true;
    }

    /// <summary>A spotter's judgement of a salvo's fall against the target, by eye: over, short or straddling, and the
    /// deflection seen (<see cref="Ladder"/> turns it into the next salvo's correction).</summary>
    public enum Sign : byte
    {
        Short,
        Over,
        Straddle,
    }

    /// <summary>The RN's ladder (fire-control-research.md §9): correct by the step until the fall crosses the target,
    /// then halve it; a straddle keeps the correction and halves the step; never under half a pattern. Each judgement is
    /// applied against the correction its salvo was fired with, so spots don't pile up (the reference's
    /// <c>engage()</c>).</summary>
    public struct Ladder
    {
        public double Step;
        public int Last;

        /// <summary>The range correction after a judgement on a salvo fired with <paramref name="firedWith"/>.</summary>
        public double Apply(Sign sign, double firedWith, double patternM, double minStepM)
        {
            double floor = Math.Max(minStepM, 0.5 * patternM);
            if (sign == Sign.Straddle)
            {
                Step = Math.Max(floor, Step / 2);
                return firedWith;
            }
            int d = sign == Sign.Over ? -1 : 1;
            if (Last != 0 && d != Last)
                Step = Math.Max(floor, Step / 2);
            Last = d;
            return firedWith + d * Step;
        }
    }
}
